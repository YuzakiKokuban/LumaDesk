"""One version resolver for local builds and tag releases; never update dependencies."""
import argparse
from dataclasses import asdict, dataclass
import json
from pathlib import Path
import re
import tomllib

ROOT = Path(__file__).resolve().parents[1]
VERSION_PATTERN = re.compile(r"(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-(alpha|beta|rc)\.([1-9]\d*))?")


@dataclass(frozen=True)
class Version:
    version: str
    file_version: str
    prerelease: bool
    branch: str

    @classmethod
    def parse(cls, value):
        match = VERSION_PATTERN.fullmatch(value)
        if not match:
            raise ValueError("Unsupported release version: use X.Y.Z or X.Y.Z-{alpha,beta,rc}.N")
        major, minor, patch = map(int, match.group(1, 2, 3))
        stage, number = match.group(4, 5)
        if max(major, minor, patch) > 65535 or (number and int(number) > 9999):
            raise ValueError("Version exceeds Windows file version limits")
        # Windows Explorer compares four numeric fields. A finished release must
        # sort above its candidates, and alpha < beta < rc must also hold.
        revision = 65535 if not stage else {"alpha": 10000, "beta": 20000, "rc": 30000}[stage] + int(number)
        return cls(value, f"{major}.{minor}.{patch}.{revision}", bool(stage), "dev" if stage else "main")

    @classmethod
    def from_tag(cls, tag):
        if not tag.startswith("v"):
            raise ValueError("Release tag must start with v")
        return cls.parse(tag[1:])


def source_version(root):
    return tomllib.loads((root / "Cargo.toml").read_text(encoding="utf-8"))["package"]["version"]


def replace_once(pattern, replacement, text):
    result, count = re.subn(pattern, replacement, text, flags=re.MULTILINE)
    if count != 1:
        raise ValueError(f"Expected one version field, found {count}: {pattern}")
    return result


def apply_version(root, info):
    cargo = root / "Cargo.toml"
    lock = root / "Cargo.lock"
    project = root / "app/JiYaoChu.csproj"
    manifest = cargo.read_text(encoding="utf-8")
    package_name = tomllib.loads(manifest)["package"]["name"]
    # Restrict the manifest edit to the package section, not dependency versions.
    manifest = replace_once(
        r'(\[package\][\s\S]*?^version\s*=\s*")[^"]+("\s*$)',
        lambda match: match[1] + info.version + match[2], manifest)
    locked = lock.read_text(encoding="utf-8")
    locked = replace_once(
        r'(\[\[package\]\]\s*\nname = "' + re.escape(package_name) + r'"\s*\nversion = ")[^"]+("\s*$)',
        lambda match: match[1] + info.version + match[2], locked)
    frontend = project.read_text(encoding="utf-8")
    frontend = replace_once(r"(<Version>)[^<]+(</Version>)", lambda match: match[1] + info.version + match[2], frontend)
    frontend = replace_once(r"(<FileVersion>)[^<]+(</FileVersion>)", lambda match: match[1] + info.file_version + match[2], frontend)
    # Validate every field before writing anything; dependencies stay byte-for-byte.
    for path, updated in ((cargo, manifest), (lock, locked), (project, frontend)):
        if path.read_text(encoding="utf-8") != updated:
            path.write_text(updated, encoding="utf-8", newline="\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT)
    choice = parser.add_mutually_exclusive_group()
    choice.add_argument("--tag")
    choice.add_argument("--version")
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--file-version-only", action="store_true")
    parser.add_argument("--github-output", type=Path)
    args = parser.parse_args()
    try:
        info = Version.from_tag(args.tag) if args.tag is not None else Version.parse(args.version or source_version(args.root))
        if args.apply:
            apply_version(args.root, info)
        if args.github_output:
            with args.github_output.open("a", encoding="utf-8", newline="\n") as output:
                for key, value in asdict(info).items():
                    output.write(f"{key}={str(value).lower() if isinstance(value, bool) else value}\n")
        print(info.file_version if args.file_version_only else json.dumps(asdict(info)))
    except (ValueError, KeyError, OSError) as error:
        parser.exit(1, f"{error}\n")


if __name__ == "__main__":
    main()
