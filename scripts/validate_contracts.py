#!/usr/bin/env python3
"""Validate CIAI machine-readable contracts and every published YAML sample."""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any
from urllib.parse import unquote

import yaml
from jsonschema import Draft202012Validator, FormatChecker


ROOT = Path(__file__).resolve().parents[1]
ENDPOINTS = {
    "/Info",
    "/HeartBeat",
    "/Function",
    "/Operation",
    "/Set",
    "/Get",
    "/EnterAndExit",
}
APPLICATION_SAMPLES = (
    "CiaiControllerSDK/application.sample.yml",
    "CiaiControllerSDKForJava/src/main/resources/application.sample.yml",
    "examples/csharp-temperature/application.yml",
    "examples/java-temperature/src/main/resources/application.yml",
)


def read_json(relative_path: str) -> Any:
    with (ROOT / relative_path).open(encoding="utf-8") as stream:
        return json.load(stream)


def read_yaml(relative_path: str) -> Any:
    with (ROOT / relative_path).open(encoding="utf-8") as stream:
        return yaml.safe_load(stream)


def format_path(error: Any) -> str:
    path = ".".join(str(part) for part in error.absolute_path)
    return path or "<root>"


def validate_markdown_links() -> None:
    missing: list[str] = []
    for markdown in ROOT.rglob("*.md"):
        if any(part in {".git", "bin", "obj", "target"} for part in markdown.parts):
            continue
        text = markdown.read_text(encoding="utf-8")
        for target in re.findall(r"\[[^\]]*\]\(([^)]+)\)", text):
            target = target.strip().split()[0].strip("<>")
            if not target or target.startswith(("#", "http://", "https://", "mailto:")):
                continue
            path = unquote(target.split("#", 1)[0])
            if path and not (markdown.parent / path).resolve().exists():
                missing.append(f"{markdown.relative_to(ROOT)}: {target}")
    if missing:
        raise AssertionError("Missing Markdown links:\n" + "\n".join(missing))


def main() -> None:
    message_schema = read_json("schemas/ciai-2.0.schema.json")
    application_schema = read_json("schemas/application.schema.json")
    Draft202012Validator.check_schema(message_schema)
    Draft202012Validator.check_schema(application_schema)

    openapi = read_yaml("openapi/ciai-2.0.yaml")
    if openapi.get("openapi") != "3.1.0":
        raise AssertionError("OpenAPI version must be 3.1.0")
    paths = set(openapi.get("paths", {}))
    if paths != ENDPOINTS:
        raise AssertionError(
            f"OpenAPI endpoint mismatch: missing={sorted(ENDPOINTS - paths)}, "
            f"extra={sorted(paths - ENDPOINTS)}"
        )

    validator = Draft202012Validator(
        application_schema, format_checker=FormatChecker()
    )
    for relative_path in APPLICATION_SAMPLES:
        document = read_yaml(relative_path)
        errors = sorted(
            validator.iter_errors(document), key=lambda error: list(error.absolute_path)
        )
        if errors:
            details = "; ".join(
                f"{format_path(error)}: {error.message}" for error in errors
            )
            raise AssertionError(f"{relative_path}: {details}")

    read_yaml(".github/workflows/ci.yml")
    read_yaml(".agents/skills/device-driver-migration/agents/openai.yaml")
    read_json(".agents/skills/device-driver-migration/evals/evals.json")
    validate_markdown_links()
    print(
        f"CIAI contract validation passed: {len(ENDPOINTS)} endpoints, "
        f"{len(APPLICATION_SAMPLES)} application.yml samples."
    )


if __name__ == "__main__":
    main()
