#!/usr/bin/env python3
"""Create a deterministic, read-only inventory for a device-driver source tree."""

from __future__ import annotations

import argparse
import json
import re
from collections import Counter
from pathlib import Path


SKIP_DIRS = {".git", ".idea", ".vs", "bin", "obj", "target", "node_modules", "packages"}
TEXT_SUFFIXES = {
    ".cs", ".java", ".kt", ".cpp", ".c", ".h", ".hpp", ".py", ".xml", ".json",
    ".yml", ".yaml", ".properties", ".config", ".md", ".txt", ".sln", ".csproj", ".fsproj",
}
BINARY_SUFFIXES = {".dll", ".exe", ".ocx", ".so", ".dylib", ".jar", ".lib"}
KEYWORDS = {
    "tcp": re.compile(r"\b(tcpclient|socket|serversocket|tcp)\b", re.I),
    "serial": re.compile(r"\b(serialport|jserialcomm|rs[- ]?232|com\d+)\b", re.I),
    "http": re.compile(r"\b(httpclient|restsharp|httpurlconnection|https?://)\b", re.I),
    "dll_com": re.compile(r"\b(dllimport|loadlibrary|comvisible|activex|interop|clsid|progid)\b", re.I),
    "file_io": re.compile(r"\b(file\.write|file\.read|filesystemwatcher|watchservice|csv|xml)\b", re.I),
    "state": re.compile(r"\b(status|state|idle|ready|running|paused|finished|completed|error|abort)\b", re.I),
    "cancellation": re.compile(r"\b(cancellationtoken|cancel|abort|terminate|stop)\b", re.I),
    "events": re.compile(r"\b(eventhandler|addlistener|subscribe|notify|progress)\b", re.I),
}


def iter_files(root: Path):
    for path in sorted(root.rglob("*"), key=lambda item: str(item).lower()):
        if not path.is_file() or any(part.lower() in SKIP_DIRS for part in path.parts):
            continue
        yield path


def inspect(root: Path) -> dict:
    extensions: Counter[str] = Counter()
    keyword_files = {name: [] for name in KEYWORDS}
    manifests, configs, binaries, likely_entries = [], [], [], []
    files_scanned = 0

    for path in iter_files(root):
        rel = path.relative_to(root).as_posix()
        suffix = path.suffix.lower()
        extensions[suffix or "<none>"] += 1
        if suffix in BINARY_SUFFIXES:
            binaries.append(rel)
        if path.name.lower() in {"pom.xml", "build.gradle", "build.gradle.kts"} or suffix in {".sln", ".csproj", ".fsproj"}:
            manifests.append(rel)
        if path.name.lower() in {"application.yml", "application.yaml", "app.config", "web.config"} or suffix in {".yml", ".yaml", ".config", ".properties"}:
            configs.append(rel)
        if path.name.lower() in {"program.cs", "main.java", "application.java"} or "controller" in path.stem.lower() or "driver" in path.stem.lower():
            likely_entries.append(rel)
        if suffix not in TEXT_SUFFIXES or path.stat().st_size > 2_000_000:
            continue
        try:
            text = path.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        files_scanned += 1
        for name, pattern in KEYWORDS.items():
            if pattern.search(text):
                keyword_files[name].append(rel)

    return {
        "root": str(root.resolve()),
        "files_scanned_as_text": files_scanned,
        "extensions": dict(sorted(extensions.items())),
        "project_manifests": manifests,
        "configuration_files": configs,
        "vendor_binaries": binaries,
        "likely_entry_or_driver_files": likely_entries,
        "evidence_files_by_topic": keyword_files,
        "notes": [
            "Keyword matches are navigation hints, not proof of behavior.",
            "Generated/build directories and text files larger than 2 MB are skipped.",
            "Binary compatibility, protocol semantics and task success conditions require separate evidence.",
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", help="Old driver, SDK example, or target source directory")
    parser.add_argument("--output", help="Optional JSON output path; stdout is used when omitted")
    args = parser.parse_args()
    root = Path(args.source)
    if not root.is_dir():
        parser.error(f"source is not a directory: {root}")
    payload = json.dumps(inspect(root), ensure_ascii=False, indent=2)
    if args.output:
        Path(args.output).write_text(payload + "\n", encoding="utf-8")
    else:
        print(payload)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
