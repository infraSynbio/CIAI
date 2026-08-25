#!/usr/bin/env python3
"""Validate this skill's deterministic package using only the Python standard library."""

from __future__ import annotations

import json
import re
from pathlib import Path


def main() -> int:
    skill_root = Path(__file__).resolve().parent.parent
    skill_file = skill_root / "SKILL.md"
    text = skill_file.read_text(encoding="utf-8")
    frontmatter = re.match(r"^---\n(.*?)\n---\n", text, re.S)
    if not frontmatter:
        raise SystemExit("SKILL.md must start with YAML frontmatter")
    name = re.search(r"^name:\s*(.+)$", frontmatter.group(1), re.M)
    description = re.search(r"^description:\s*(.+)$", frontmatter.group(1), re.M)
    if not name or name.group(1).strip() != "device-driver-migration":
        raise SystemExit("frontmatter name mismatch")
    if not description or len(description.group(1).strip()) < 40:
        raise SystemExit("frontmatter description is missing or too vague")

    missing = []
    for target in re.findall(r"\]\((references/[^)#]+)", text):
        if not (skill_root / target).is_file():
            missing.append(target)
    if missing:
        raise SystemExit("missing referenced files: " + ", ".join(sorted(set(missing))))

    eval_path = skill_root / "evals" / "evals.json"
    evals = json.loads(eval_path.read_text(encoding="utf-8"))
    if evals.get("skill_name") != "device-driver-migration":
        raise SystemExit("eval skill_name mismatch")
    cases = evals.get("evals", [])
    ids = [case.get("id") for case in cases]
    if len(cases) < 12 or len(ids) != len(set(ids)):
        raise SystemExit("evals require at least 12 unique cases")
    for case in cases:
        if not case.get("prompt") or not case.get("expected_output"):
            raise SystemExit(f"eval {case.get('id')} is incomplete")

    inventory = skill_root / "scripts" / "inventory_driver.py"
    if not inventory.is_file():
        raise SystemExit("inventory_driver.py is missing")
    compile(inventory.read_text(encoding="utf-8"), str(inventory), "exec")
    print(f"device-driver-migration skill validation passed ({len(cases)} evals)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
