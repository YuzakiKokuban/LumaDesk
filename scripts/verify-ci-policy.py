"""Check that splitting CI cannot silently bypass release or merge requirements."""
from pathlib import Path
import copy
import unittest
import yaml

ROOT = Path(__file__).resolve().parents[1]


def check(ci, release, build, checks, audit):
    gate = ci["jobs"]["windows"]
    if gate.get("name") != "Windows x64" or gate.get("if") != "always()" or set(gate["needs"]) != {"checks", "audit", "package"}:
        raise ValueError("Merge gate must aggregate every check, including skipped failures")
    if "!= 'success'" not in gate["steps"][0]["run"]:
        raise ValueError("Merge gate must reject every non-success result")
    publish = release["jobs"]["publish"]
    if set(publish["needs"]) != {"metadata", "checks", "audit", "package"} or publish.get("if"):
        raise ValueError("Publish must wait for successful provenance, checks, audit and packages")
    provenance = release["jobs"]["metadata"]["steps"]
    if not any("verify-release.ps1" in step.get("run", "") for step in provenance):
        raise ValueError("Release provenance validation is missing")
    for key in ("checks", "package"):
        caller = release["jobs"][key]
        if caller["with"]["version"] != "${{ needs.metadata.outputs.version }}":
            raise ValueError("Every release build must use the tag version")
    for workflow in (build, checks, audit):
        if "workflow_call" not in workflow["on"]:
            raise ValueError("Shared verification must be reusable")
    commands = "\n".join(step.get("run", "") for step in build["jobs"]["package"]["steps"])
    for required in ("verify-package.py", "verify-shell.ps1", "verify-installer.ps1", "verify-system-osd.ps1", "verify-installer-dependencies.py", "verify-autostart-migration.py", "-warnaserror"):
        if required not in commands:
            raise ValueError(f"Required Windows verification missing: {required}")
    if release["concurrency"]["cancel-in-progress"] != "false":
        raise ValueError("Release runs must not cancel an in-progress publication")


def load():
    return tuple(yaml.load((ROOT / ".github/workflows" / name).read_text(encoding="utf-8"), Loader=yaml.BaseLoader)
                 for name in ("ci.yml", "release.yml", "build.yml", "checks.yml", "dependency-audit.yml"))


class PolicyTests(unittest.TestCase):
    def setUp(self):
        self.workflows = copy.deepcopy(load())

    def test_current_contract(self):
        check(*self.workflows)

    def test_audit_cannot_be_removed_from_publication(self):
        self.workflows[1]["jobs"]["publish"]["needs"].remove("audit")
        with self.assertRaises(ValueError):
            check(*self.workflows)

    def test_publish_cannot_run_after_failed_checks(self):
        self.workflows[1]["jobs"]["publish"]["if"] = "always()"
        with self.assertRaises(ValueError):
            check(*self.workflows)

    def test_skipped_merge_gate_cannot_look_successful(self):
        del self.workflows[0]["jobs"]["windows"]["if"]
        with self.assertRaises(ValueError):
            check(*self.workflows)

    def test_installer_checks_cannot_be_dropped(self):
        steps = self.workflows[2]["jobs"]["package"]["steps"]
        steps[:] = [step for step in steps if "verify-installer.ps1" not in step.get("run", "")]
        with self.assertRaises(ValueError):
            check(*self.workflows)

    def test_release_build_cannot_use_development_version(self):
        self.workflows[1]["jobs"]["package"]["with"]["version"] = ""
        with self.assertRaises(ValueError):
            check(*self.workflows)


if __name__ == "__main__":
    unittest.main()
