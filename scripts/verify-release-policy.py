"""Exercise release guards in isolated local Git repositories without publishing."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


SCRIPT = Path(__file__).with_name("verify-release.ps1").resolve()


class ReleasePolicyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="lumadesk-release-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.remote = self.root / "remote.git"
        self.repo = self.root / "work"
        self.repo.mkdir()
        self.git("init", "--bare", str(self.remote))
        self.git("init", "-b", "main")
        self.git("config", "user.name", "Release policy test")
        self.git("config", "user.email", "test@example.invalid")
        self.git("config", "commit.gpgsign", "false")
        self.git("config", "tag.gpgsign", "false")
        self.git("remote", "add", "origin", str(self.remote))
        self.git("commit", "--allow-empty", "-m", "base")
        self.git("branch", "dev")
        self.git("push", "origin", "main", "dev")

    def git(self, *args):
        return subprocess.run(["git", *args], cwd=self.repo, check=True,
                              capture_output=True, encoding="utf-8", errors="replace").stdout.strip()

    def release(self, version, branch):
        self.git("switch", branch)
        (self.repo / "Cargo.toml").write_text(
            f'[package]\nversion = "{version}"\n', encoding="utf-8")
        notes = self.repo / "docs/releases" / f"{version}.md"
        notes.parent.mkdir(parents=True, exist_ok=True)
        notes.write_text("Release notes\n", encoding="utf-8")
        self.git("add", ".")
        self.git("commit", "-m", "release")
        self.git("tag", f"v{version}")
        self.git("push", "origin", branch)
        return f"v{version}"

    def verify(self, tag, error=None):
        result = subprocess.run(
            [shutil.which("pwsh") or "powershell", "-NoProfile", "-File", str(SCRIPT), "-Tag", tag],
            cwd=self.repo, capture_output=True, encoding="utf-8", errors="replace")
        output = result.stdout + result.stderr
        if error:
            self.assertNotEqual(result.returncode, 0, output)
            self.assertIn(error, output)
        else:
            self.assertEqual(result.returncode, 0, output)

    def test_stable_on_main(self):
        self.verify(self.release("1.0.0", "main"))

    def test_beta_on_dev(self):
        self.verify(self.release("1.0.0-beta.1", "dev"))

    def test_stable_on_dev_rejected(self):
        self.verify(self.release("1.0.0", "dev"), "must belong to main")

    def test_beta_on_main_rejected(self):
        self.verify(self.release("1.0.0-beta.1", "main"), "must belong to dev")

    def test_tag_versions_the_release_without_manual_source_bump(self):
        self.release("1.0.0", "main")
        self.git("tag", "v1.0.1")
        (self.repo / "docs/releases/NOTES.md").write_text("Maintainer verification notes\n", encoding="utf-8")
        self.verify("v1.0.1")

    def test_invalid_semver_rejected_before_publication(self):
        self.release("1.0.0", "main")
        self.verify("v1.00.0", "Unsupported release tag")

    def test_wrong_checkout_rejected(self):
        tag = self.release("1.0.0", "main")
        self.git("commit", "--allow-empty", "-m", "later")
        self.verify(tag, "Checkout does not match release tag")

    def test_missing_notes_rejected(self):
        tag = self.release("1.0.0", "main")
        (self.repo / "docs/releases/1.0.0.md").unlink()
        self.verify(tag, "Release notes are missing")


if __name__ == "__main__":
    unittest.main()
