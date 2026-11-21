#!/usr/bin/env python3
"""Verify that project paths referenced by GitHub workflows still exist.

Why this exists
---------------
`GITHUB_TOKEN` is a GitHub App installation token and cannot be granted the
`workflows` permission, so the upstream-sync job keeps the fork's own
`.github/workflows/*` snapshots (see "Preserve fork workflow files" in
.github/workflows/upstream-sync.yml). A snapshot keeps working only while the
paths it mentions still exist. When upstream renames a project (for example
`v2rayN/v2rayN/` -> `v2rayN/v2rayN.Windows/`), every build keeps compiling
nothing but the old path and fails hours later, in a release run.

This check runs at the end of the sync job so the breakage is reported in the
run that caused it, with the exact file, line and replacement candidate.

Usage:
    python3 scripts/check-ci-contract.py [--root DIR] [--report FILE] [--quiet]

Exit status: 0 when every referenced project exists, 1 otherwise.
"""

from __future__ import annotations

import argparse
import os
import re
import sys
from pathlib import Path

WORKFLOW_DIR = Path(".github/workflows")
PROJECT_SUFFIX = ".csproj"
TOKEN_RE = re.compile(r"[A-Za-z0-9_.$/{}-]*\.csproj")
# Stems that are obviously not project file references (YAML anchors etc.).
SKIP_PREFIXES = ("${{",)


def find_projects(root: Path) -> list[Path]:
    return sorted(p.relative_to(root) for p in root.rglob("*" + PROJECT_SUFFIX))


def candidate_bases(raw: str) -> list[Path]:
    """Where a workflow path may be anchored.

    Reusable build steps run with `working-directory: ./v2rayN`, so the same
    string is valid either from the repository root or from `v2rayN/`:
      ./v2rayN/v2rayN.Windows/v2rayN.Windows.csproj   (root)
      ./v2rayN.Windows/v2rayN.Windows.csproj          (v2rayN/)
    """
    cleaned = raw.strip("'\"")
    if ":" in cleaned:  # e.g. windows:// or a trailing comment
        cleaned = cleaned.split(":", 1)[0]
    return [Path(cleaned.lstrip("./")), Path("v2rayN") / cleaned.lstrip("./")]


def resolve(root: Path, raw: str) -> Path | None:
    for base in candidate_bases(raw):
        if (root / base).is_file():
            return base
    return None


def assembly_name(project: Path, root: Path) -> str | None:
    try:
        text = (root / project).read_text(encoding="utf-8", errors="replace")
    except OSError:
        return None
    match = re.search(r"<AssemblyName>\s*([^<\s]+)\s*</AssemblyName>", text)
    return match.group(1) if match else None


def suggest(raw: str, projects: list[Path], root: Path) -> dict:
    """Return replacement paths for `raw`, in root form and in `v2rayN/` form.

    Upstream renames keep the assembly name (v2rayN/v2rayN.csproj ->
    v2rayN/v2rayN.Windows/v2rayN.Windows.csproj), so assembly-name matching
    pinpoints the new file even when the file name changed. Several projects can
    share an assembly name (the WPF and Avalonia front ends both build
    `v2rayN.exe`), so the reason is reported with the candidates.
    """
    stem = Path(raw.strip("'\"")).stem
    matches = [p for p in projects if (assembly_name(p, root) or p.stem) == stem]
    reason = f"same AssemblyName as the missing file ({stem})"
    if not matches:
        matches = [p for p in projects if p.stem == stem]
        reason = "same file name as the missing file"
    paths: list[str] = []
    for p in matches[:3]:
        paths.append(f"./{p.as_posix()}")
        if p.parts and p.parts[0] == "v2rayN":
            paths.append("./" + Path(*p.parts[1:]).as_posix())
    return {"reason": reason, "paths": paths}


def scan(root: Path) -> list[dict]:
    projects = find_projects(root)
    problems: list[dict] = []
    for wf in sorted((root / WORKFLOW_DIR).glob("*.y*ml")):
        text = wf.read_text(encoding="utf-8", errors="replace")
        for lineno, line in enumerate(text.splitlines(), start=1):
            for raw in TOKEN_RE.findall(line):
                if raw.startswith(SKIP_PREFIXES):
                    continue
                if resolve(root, raw) is not None:
                    continue
                problems.append(
                    {
                        "file": wf.relative_to(root).as_posix(),
                        "line": lineno,
                        "path": raw,
                        "suggestions": suggest(raw, projects, root),
                    }
                )
    return problems


def render(problems: list[dict], projects: list[Path]) -> str:
    out = ["### CI path contract: referenced projects that no longer exist", ""]
    for p in problems:
        out.append(f"- `{p['file']}:{p['line']}` -> `{p['path']}`")
        if p["suggestions"]["paths"]:
            out.append(f"  - candidates ({p['suggestions']['reason']}):")
            for s in p["suggestions"]["paths"]:
                out.append(f"    - `{s}`")
    if not problems:
        out = ["### CI path contract: OK", ""]
        out.append(f"All referenced projects exist ({len(projects)} project files).")
    out += ["", "Current project files:", ""]
    out += [f"- `{p.as_posix()}`" for p in projects]
    return "\n".join(out)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".", help="repository root")
    ap.add_argument("--report", help="write the Markdown report to this file")
    ap.add_argument("--quiet", action="store_true", help="only print on failure")
    args = ap.parse_args()

    root = Path(args.root).resolve()
    problems = scan(root)
    projects = find_projects(root)
    report = render(problems, projects)

    if args.report:
        Path(args.report).write_text(report + "\n", encoding="utf-8")
    step_summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if step_summary:
        with open(step_summary, "a", encoding="utf-8") as fh:
            fh.write(report + "\n")

    for p in problems:
        hint = ", ".join(p["suggestions"]["paths"]) or "see project list in the report"
        print(
            f"::error file={p['file']},line={p['line']}::"
            f"{p['path']} does not exist (candidate: {hint})"
        )

    if problems:
        print(report)
        return 1

    if not args.quiet:
        print(f"CI path contract OK ({len(projects)} project files, all references resolve).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
