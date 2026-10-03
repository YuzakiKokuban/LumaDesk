"""Require exactly the versioned release files and verify both SHA256 sidecars."""
import argparse
import hashlib
from pathlib import Path
import re
from version import Version


def verify(directory, info):
    packages = [f"LumaDesk-{info.version}-win-x64.zip", f"LumaDesk-{info.version}-win-x64-Setup.exe"]
    expected = {name for package in packages for name in (package, package + ".sha256")}
    entries = list(directory.iterdir())
    if {path.name for path in entries} != expected or any(not path.is_file() or path.is_symlink() for path in entries):
        raise ValueError("Release must contain exactly the matching ZIP, installer and SHA256 files")
    for name in packages:
        package = directory / name
        sidecar = (directory / (name + ".sha256")).read_text(encoding="utf-8-sig").strip()
        match = re.fullmatch(r"([0-9a-fA-F]{64})  (.+)", sidecar)
        if not match or match[2] != name or package.stat().st_size == 0:
            raise ValueError(f"Invalid release checksum metadata: {name}")
        if match[1].lower() != hashlib.sha256(package.read_bytes()).hexdigest():
            raise ValueError(f"Release checksum mismatch: {name}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", type=Path, required=True)
    parser.add_argument("--version", required=True)
    args = parser.parse_args()
    verify(args.directory, Version.parse(args.version))
    print("Verified release asset names, completeness and SHA256")
