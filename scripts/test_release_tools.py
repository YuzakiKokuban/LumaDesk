import hashlib
import importlib.util
from pathlib import Path
import tempfile
import tomllib
import unittest
from version import Version, apply_version


def load(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


assets = load("verify-release-assets")
audit = load("verify-nuget-audit")
notes = load("release-notes")


class VersionTests(unittest.TestCase):
    def test_candidates_sort_before_their_release(self):
        versions = ["0.2.0-alpha.9", "0.2.0-beta.1", "0.2.0-rc.1", "0.2.0"]
        numbers = [tuple(map(int, Version.parse(value).file_version.split('.'))) for value in versions]
        self.assertEqual(numbers, sorted(set(numbers)))
        self.assertEqual(Version.from_tag("v0.2.0").branch, "main")
        self.assertEqual(Version.from_tag("v0.2.0-rc.1").branch, "dev")

    def test_invalid_or_ambiguous_tags_fail(self):
        for tag in ("0.2.0", "v0.02.0", "v0.2.00", "v0.2.0-beta.01", "v0.2.0+build.1", "v0.2.0-preview.1", "v0.2.0-beta.0", "v0.2.0-beta.10000", "v65536.0.0", "v0.2.0\nversion=bad"):
            with self.subTest(tag=tag), self.assertRaises(ValueError):
                Version.from_tag(tag)

    def test_apply_updates_only_the_root_package(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "app").mkdir()
            (root / "Cargo.toml").write_text('[package]\nname = "jiyaochu"\nversion = "0.2.0-beta.4"\n\n[dependencies]\nserde = "1"\n')
            (root / "Cargo.lock").write_text('version = 4\n\n[[package]]\nname = "jiyaochu"\nversion = "0.2.0-beta.4"\n\n[[package]]\nname = "serde"\nversion = "1.0.999"\nchecksum = "unchanged"\n')
            (root / "app/JiYaoChu.csproj").write_text('<Project><Version>0.2.0-beta.4</Version><FileVersion>0.2.0.4</FileVersion></Project>')
            before = tomllib.loads((root / "Cargo.lock").read_text())["package"][1]
            apply_version(root, Version.parse("0.2.0"))
            self.assertEqual(tomllib.loads((root / "Cargo.lock").read_text())["package"][1], before)
            self.assertIn('<FileVersion>0.2.0.65535</FileVersion>', (root / "app/JiYaoChu.csproj").read_text())
            result = (root / "Cargo.lock").read_bytes()
            apply_version(root, Version.parse("0.2.0"))
            self.assertEqual((root / "Cargo.lock").read_bytes(), result)

    def test_invalid_project_prevents_partial_version_changes(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "app").mkdir()
            manifest = '[package]\nname = "jiyaochu"\nversion = "0.2.0-beta.4"\n'
            (root / "Cargo.toml").write_text(manifest)
            (root / "Cargo.lock").write_text('[[package]]\nname = "jiyaochu"\nversion = "0.2.0-beta.4"\n')
            (root / "app/JiYaoChu.csproj").write_text('<Project/>')
            with self.assertRaises(ValueError):
                apply_version(root, Version.parse("0.2.0"))
            self.assertEqual((root / "Cargo.toml").read_text(), manifest)


class AssetTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.info = Version.parse("0.2.0")
        for suffix in (".zip", "-Setup.exe"):
            name = "LumaDesk-0.2.0-win-x64" + suffix
            (self.root / name).write_bytes(b"verified-package")
            (self.root / (name + ".sha256")).write_text(hashlib.sha256(b"verified-package").hexdigest() + "  " + name + "\n")

    def test_matching_packages_pass(self):
        assets.verify(self.root, self.info)

    def test_corrupt_download_fails(self):
        (self.root / "LumaDesk-0.2.0-win-x64.zip").write_bytes(b"corrupt")
        with self.assertRaises(ValueError):
            assets.verify(self.root, self.info)

    def test_missing_or_extra_assets_fail(self):
        (self.root / "extra.zip").write_bytes(b"extra")
        with self.assertRaises(ValueError):
            assets.verify(self.root, self.info)
        (self.root / "extra.zip").unlink()
        (self.root / "LumaDesk-0.2.0-win-x64-Setup.exe.sha256").unlink()
        with self.assertRaises(ValueError):
            assets.verify(self.root, self.info)


class AuditTests(unittest.TestCase):
    def report(self, **extra):
        return {"version": 1, "parameters": "--vulnerable --include-transitive", "sources": ["https://api.nuget.org/v3/index.json"], "projects": [{"path": "app.csproj", "frameworks": [{"framework": "net10.0", **extra}]}]}

    def test_complete_clean_report_passes(self):
        audit.verify(self.report())
        clean = self.report()
        del clean["projects"][0]["frameworks"]
        audit.verify(clean)

    def test_direct_and_transitive_vulnerabilities_block(self):
        for group in ("topLevelPackages", "transitivePackages"):
            report = self.report(**{group: [{"id": "unsafe", "vulnerabilities": [{"severity": "High"}]}]})
            with self.subTest(group=group), self.assertRaises(ValueError):
                audit.verify(report)

    def test_unavailable_audit_cannot_count_as_clean(self):
        for report in ({}, {"version": 1, "projects": []}, {**self.report(), "errors": ["network unavailable"]}, {**self.report(), "logs": [{"level": "error", "message": "unavailable"}]}):
            with self.assertRaises(ValueError):
                audit.verify(report)


class NotesTests(unittest.TestCase):
    def test_template_fallback_and_version_specific_override(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "docs/releases").mkdir(parents=True)
            (root / "docs/releases/NOTES.md").write_text("Hardware validation pending.")
            info = Version.parse("0.2.0")
            self.assertIn("Hardware validation pending.", notes.render(root, info, "- Fix settings"))
            (root / "docs/releases/0.2.0.md").write_text("Verified on supported hardware.")
            result = notes.render(root, info, "- Fix settings")
            self.assertIn("Verified on supported hardware.", result)
            self.assertIn("- Fix settings", result)
            self.assertNotIn("Hardware validation pending.", result)

    def test_missing_human_notes_prevent_release(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(OSError):
                notes.render(Path(directory), Version.parse("0.2.0"), "- Fix")


if __name__ == "__main__":
    unittest.main()
