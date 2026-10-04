"""Check that splitting CI cannot silently bypass release or merge requirements."""
from pathlib import Path
import copy
import unittest
import yaml

ROOT = Path(__file__).resolve().parents[1]


def check(ci, release, build, checks, audit, telegram):
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
    dev_delivery = ci["jobs"]["telegram"]
    if dev_delivery.get("if") != "github.event_name == 'push' && github.ref == 'refs/heads/dev'" or dev_delivery["needs"] != "windows":
        raise ValueError("Development delivery must only follow a successful dev push gate")
    release_delivery = release["jobs"]["telegram"]
    if set(release_delivery["needs"]) != {"metadata", "publish"} or release_delivery.get("if"):
        raise ValueError("Release delivery must follow a successful publication")
    for caller in (dev_delivery, release_delivery):
        if caller["uses"] != "./.github/workflows/telegram.yml" or set(caller["secrets"]) != {"TELEGRAM_BOT_TOKEN", "TELEGRAM_CHAT_ID"}:
            raise ValueError("Telegram credentials must remain scoped to the delivery workflow")
    for workflow in (ci, release):
        for name, job in workflow["jobs"].items():
            if name != "telegram" and "secrets" in job:
                raise ValueError("Build/check jobs must not receive delivery credentials")
    if "workflow_call" not in telegram["on"] or telegram["permissions"] != {"contents": "read"}:
        raise ValueError("Delivery must be a reusable read-only GitHub workflow")
    commands = "\n".join(step.get("run", "") for step in telegram["jobs"]["send"]["steps"])
    if "notify-telegram.py" not in commands:
        raise ValueError("Verified installer delivery is missing")


def load():
    return tuple(yaml.load((ROOT / ".github/workflows" / name).read_text(encoding="utf-8"), Loader=yaml.BaseLoader)
                 for name in ("ci.yml", "release.yml", "build.yml", "checks.yml", "dependency-audit.yml", "telegram.yml"))


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

    def test_pr_delivery_cannot_receive_credentials(self):
        del self.workflows[0]["jobs"]["telegram"]["if"]
        with self.assertRaises(ValueError):
            check(*self.workflows)

    def test_release_delivery_cannot_precede_publication(self):
        self.workflows[1]["jobs"]["telegram"]["needs"].remove("publish")
        with self.assertRaises(ValueError):
            check(*self.workflows)


if __name__ == "__main__":
    unittest.main()
