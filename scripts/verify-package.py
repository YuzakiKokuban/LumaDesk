"""Check a Windows portable archive, PE resources, and the mock FFI/CLI."""
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess
import tempfile
import tomllib
import zipfile
from version import Version

ROOT = Path(__file__).resolve().parents[1]


def u16(data, offset):
    return struct.unpack_from("<H", data, offset)[0]


def u32(data, offset):
    return struct.unpack_from("<I", data, offset)[0]


def resources(data):
    pe = u32(data, 0x3c)
    assert data[pe:pe+4] == b"PE\0\0", "Invalid PE signature"
    optional = pe + 24
    directory = optional + (112 if u16(data, optional) == 0x20b else 96)
    resource_rva = u32(data, directory + 16)
    sections = []
    section_start = optional + u16(data, pe + 20)
    for index in range(u16(data, pe + 6)):
        offset = section_start + index * 40
        sections.append((u32(data, offset+12), max(u32(data, offset+8), u32(data, offset+16)), u32(data, offset+20)))

    def file_offset(rva):
        for start, size, raw in sections:
            if start <= rva < start + size:
                return raw + rva - start
        raise ValueError(f"Invalid resource address: {rva:#x}")

    base = file_offset(resource_rva)
    result = {}

    def walk(relative, keys=()):
        node = base + relative
        for index in range(u16(data, node+12) + u16(data, node+14)):
            entry = node + 16 + 8 * index
            name, target = u32(data, entry), u32(data, entry+4)
            if target & 0x80000000:
                walk(target & 0x7fffffff, keys + (name,))
            else:
                leaf = base + target
                offset = file_offset(u32(data, leaf))
                result[keys + (name,)] = data[offset:offset+u32(data, leaf+4)]

    walk(0)
    return result


def expected_icons():
    icon = (ROOT / "assets/LumaDesk.ico").read_bytes()
    images = set()
    for index in range(u16(icon, 4)):
        entry = 6 + 16 * index
        size, offset = u32(icon, entry+8), u32(icon, entry+12)
        images.add(hashlib.sha256(icon[offset:offset+size]).digest())
    return images


def verify_version_resource(embedded, info):
    versions = [data for keys, data in embedded.items() if keys[0] == 16]
    assert versions, "PE version resource is missing"
    expected = tuple(map(int, info.file_version.split('.')))
    for data in versions:
        offset = data.index(struct.pack('<I', 0xFEEF04BD))
        ms, ls = u32(data, offset + 8), u32(data, offset + 12)
        actual = (ms >> 16, ms & 0xffff, ls >> 16, ls & 0xffff)
        assert actual == expected, f"Windows file version differs: {actual} != {expected}"
        assert info.version.encode('utf-16-le') in data, "Product version differs"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--archive", required=True)
    parser.add_argument("--installer")
    args = parser.parse_args()
    archive_path = Path(args.archive).resolve()
    version = tomllib.loads((ROOT / "Cargo.toml").read_text(encoding="utf-8"))["package"]["version"]
    info = Version.parse(version)
    assert archive_path.name == f"LumaDesk-{version}-win-x64.zip"
    checksum = archive_path.with_suffix(".zip.sha256").read_text(encoding="utf-8").split()[0]
    assert checksum == hashlib.sha256(archive_path.read_bytes()).hexdigest()
    icons = expected_icons()
    with tempfile.TemporaryDirectory(prefix="lumadesk-package-") as directory:
        folder = Path(directory)
        with zipfile.ZipFile(archive_path) as archive:
            assert archive.testzip() is None, "Archive CRC failed"
            names = {name.casefold() for name in archive.namelist()}
            required = {"LumaDesk.exe", "LumaDesk.pri", "jiyaochu_core.dll", "jiyaochu-ctl.exe",
                        "Microsoft.WindowsAppRuntime.Bootstrap.dll", "LumaDesk.runtimeconfig.json",
                        "RUNTIMES.md", "README.md", "README_en.md", "LICENSE"}
            required = {name.casefold() for name in required}
            assert required <= names, f"Missing files: {required - names}"
            assert "lumadesk.dll" in names and "lumadesk.deps.json" in names, "Managed application outputs missing"
            assert not any(name.startswith("机耀处.") for name in names), "Obsolete application outputs were bundled"
            assert not ({"coreclr.dll", "hostfxr.dll", "microsoft.ui.xaml.dll"} & names), "Shared runtimes were bundled"
            assert not ({"microsoft.ui.reactor.devtools.dll", "reactor.devtools.dll"} & names), "Debug tooling was bundled"
            runtime = json.loads(archive.read("LumaDesk.runtimeconfig.json"))
            assert runtime["runtimeOptions"]["framework"]["name"] == "Microsoft.NETCore.App"
            assert len({name.casefold() for name in archive.namelist()}) == len(archive.namelist()), "Duplicate Windows paths"
            for filename in ["LumaDesk.exe", "jiyaochu-ctl.exe"]:
                embedded = resources(archive.read(filename))
                verify_version_resource(embedded, info)
                actual = {hashlib.sha256(data).digest() for keys, data in embedded.items() if keys[0] == 3}
                assert actual == icons, f"{filename}: embedded logo differs"
                if filename == "LumaDesk.exe":
                    manifests = [data for keys, data in embedded.items() if keys[0] == 24]
                    assert any(b'level="requireAdministrator"' in data for data in manifests), "Administrator manifest missing"
            archive.extractall(folder)
        environment = os.environ.copy()
        environment.update(JIYAOCHU_FORCE_MOCK="1", JIYAOCHU_DATA_DIR=str(folder / "test-config"))
        cli = folder / "jiyaochu-ctl.exe"
        actual = subprocess.check_output([str(cli), "--version"], env=environment).decode("utf-8")
        assert f" {version} " in actual, actual
        for command in ["system.ping", "get_hardware_status", "get_lighting_state", "get_power_settings"]:
            reply = json.loads(subprocess.check_output([str(cli), command], env=environment))
            assert reply["ok"], reply
        os.environ.update(JIYAOCHU_FORCE_MOCK="1", JIYAOCHU_DATA_DIR=str(folder / "test-config"))
        library = ctypes.CDLL(str(folder / "jiyaochu_core.dll"))
        library.lumadesk_abi_version.restype = ctypes.c_uint32
        assert library.lumadesk_abi_version() == 1
        # Release the DLL before TemporaryDirectory removes the extracted package.
        ctypes.windll.kernel32.FreeLibrary.argtypes = [ctypes.c_void_p]
        ctypes.windll.kernel32.FreeLibrary(ctypes.c_void_p(library._handle))
    if args.installer:
        installer = Path(args.installer)
        assert installer.name == f"LumaDesk-{version}-win-x64-Setup.exe", "Installer name differs"
        verify_version_resource(resources(installer.read_bytes()), info)
        checksum = Path(str(installer) + '.sha256').read_text(encoding='utf-8').split()[0]
        assert checksum == hashlib.sha256(installer.read_bytes()).hexdigest(), "Installer checksum differs"
    print(f"Verified LumaDesk {version}: archive, PE versions, icons, administrator manifest, CLI, FFI")


if __name__ == "__main__":
    main()
