"""Fail on NuGet vulnerabilities, errors, or an incomplete JSON audit."""
import json
from pathlib import Path
import sys


def verify(report):
    if report.get("version") != 1 or not report.get("projects") or not report.get("sources"):
        raise ValueError("Incomplete NuGet audit report")
    if not all(option in report.get("parameters", "") for option in ("--vulnerable", "--include-transitive")):
        raise ValueError("NuGet report must audit direct and transitive vulnerabilities")
    if report.get("errors") or any(str(log.get("level", "")).lower() == "error" for log in report.get("logs", [])):
        raise ValueError(f"NuGet audit failed: {report.get('errors') or report.get('logs')}")
    findings = []
    for project in report["projects"]:
        if not project.get("path") or project.get("errors") or any(str(log.get("level", "")).lower() == "error" for log in project.get("logs", [])):
            raise ValueError(f"NuGet project audit failed: {project.get('path')}")
        # --vulnerable omits frameworks altogether when no packages are affected.
        for framework in project.get("frameworks", []):
            for group in ("topLevelPackages", "transitivePackages"):
                for package in framework.get(group, []):
                    for advisory in package.get("vulnerabilities", []):
                        findings.append(f"{package['id']} {package.get('resolvedVersion', '')}: {advisory.get('severity')} {advisory.get('advisoryurl')}")
    if findings:
        raise ValueError("Vulnerable NuGet packages:\n" + "\n".join(findings))


if __name__ == "__main__":
    try:
        verify(json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig")))
        print("NuGet audit passed (direct and transitive dependencies)")
    except (ValueError, KeyError, OSError) as error:
        raise SystemExit(str(error))
