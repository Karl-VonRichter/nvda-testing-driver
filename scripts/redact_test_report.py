"""Produce a shareable result summary; retain TRX and metadata as private evidence.

Use an allowlist instead of trying to recognise every possible piece of private speech.
No error messages, stack traces, attachments, stdout, or captured speech are copied.
"""
import argparse
from collections import Counter
from datetime import datetime
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET


NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
OUTCOMES = {"Passed", "Failed", "Inconclusive", "NotExecuted", "Timeout", "Aborted", "Error", "NotRunnable", "Disconnected", "Warning"}
RUN_OUTCOMES = OUTCOMES | {"Completed", "InProgress", "Pending"}


def safe_browser(value):
    return value if isinstance(value, str) and value.casefold() in {"chrome", "chromium", "edge", "msedge", "microsoftedge", "firefox", "safari", "unavailable"} else "[redacted]"


def safe_version(value):
    return value if isinstance(value, str) and (value == "unavailable" or
        re.fullmatch(r"[0-9]{1,9}(?:\.[0-9]{1,9}){1,3}(?:[-+]?(?:alpha|beta|rc|dev)[0-9]*)?", value, re.IGNORECASE)) else "[redacted]"


def safe_revision(value):
    return value if isinstance(value, str) and (re.fullmatch(r"[0-9a-fA-F]{7,64}", value) or value == "unavailable") else "[redacted]"


def safe_timestamp(value):
    if value == "unavailable":
        return value
    if isinstance(value, str) and re.fullmatch(r"[0-9TtZz:+. -]{10,40}", value):
        try:
            datetime.fromisoformat(value.replace("Z", "+00:00"))
            return value
        except ValueError:
            pass
    return "[redacted]"


def make_report(trx, metadata):
    root = ET.parse(trx).getroot()
    if root.tag != "{" + NS["t"] + "}TestRun":
        raise ValueError("Expected a Visual Studio TRX TestRun document.")
    run_id = metadata.get("RunId")
    if not isinstance(run_id, str) or not re.fullmatch(r"[0-9a-f]{32}", run_id):
        raise ValueError("Expected the private harness run ID in metadata.")
    referenced_ids = set()
    for result_file in root.findall(".//t:ResultFile", NS):
        filename = re.split(r"[\\/]", result_file.get("path", ""))[-1]
        match = re.fullmatch(r"run-([0-9a-f]{32})\.json", filename, re.IGNORECASE)
        if match:
            referenced_ids.add(match.group(1).lower())
    for output in root.findall(".//t:StdOut", NS):
        referenced_ids.update(re.findall(r"(?m)^Private run metadata ID: ([0-9a-f]{32})\s*$", output.text or ""))
    if referenced_ids != {run_id}:
        raise ValueError("Run metadata is missing, ambiguous, or belongs to a different TRX run.")
    methods = {}
    for definition in root.findall("t:TestDefinitions/t:UnitTest", NS):
        method = definition.find("t:TestMethod", NS)
        name = method.get("name", "") if method is not None else ""
        # Data-row display names can contain emails or other customer data; use only the method identifier.
        methods[definition.get("id")] = name if re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]{0,150}", name) else "[redacted test name]"
    results = []
    for result in root.findall("t:Results/t:UnitTestResult", NS):
        outcome = result.get("outcome")
        message = result.find("t:Output/t:ErrorInfo/t:Message", NS)
        # MSTest encodes Assert.Inconclusive as NotExecuted. Recognise only its fixed prefix; never copy the message.
        if outcome == "NotExecuted" and message is not None and (message.text or "").startswith("Assert.Inconclusive failed."):
            outcome = "Inconclusive"
        results.append((methods.get(result.get("testId"), "[redacted test name]"), outcome if outcome in OUTCOMES else "Unknown"))
    if not results:
        raise ValueError("The TRX contains no test results; do not publish an empty success summary.")
    summary = root.find("t:ResultSummary", NS)
    run_outcome = summary.get("outcome") if summary is not None else None
    run_events = Counter()
    if summary is not None:
        for event in summary.findall("t:RunInfos/t:RunInfo", NS):
            outcome = event.get("outcome")
            run_events[outcome if outcome in OUTCOMES else "Unknown"] += 1
    lines = ["NVDA test results (redacted summary)",
             "Run outcome: " + (run_outcome if run_outcome in RUN_OUTCOMES else "Unknown"),
             "Run-level incidents: " + (", ".join(f"{name}={run_events[name]}" for name in sorted(run_events)) or "none recorded"),
             "Browser: " + safe_browser(metadata.get("Browser")),
             "Browser version: " + safe_version(metadata.get("BrowserVersion")),
             "NVDA version: " + safe_version(metadata.get("NvdaVersion")),
             "Deployed revision: " + safe_revision(metadata.get("Revision")),
             "Build timestamp: " + safe_timestamp(metadata.get("BuildTimestamp")),
             "Run started: " + safe_timestamp(metadata.get("StartedUtc")),
             "URL: [redacted]", "Organisation: [redacted]", "",
             "Raw speech, errors, profile data, paths, and attachments are omitted.", ""]
    counts = Counter(outcome for _, outcome in results)
    lines.append("Results: " + ", ".join(f"{name}={counts[name]}" for name in sorted(counts)))
    lines.extend(f"{outcome}: {name}" for name, outcome in results)
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("trx", type=Path)
    parser.add_argument("--metadata", type=Path, required=True, help="Private run JSON attached to this same test run")
    parser.add_argument("--output", type=Path, required=True, help="Redacted plain text output")
    args = parser.parse_args()
    if args.output.resolve() in {args.trx.resolve(), args.metadata.resolve()}:
        parser.error("The output must not overwrite private evidence.")
    try:
        metadata = json.loads(args.metadata.read_text(encoding="utf-8-sig"))
        if not isinstance(metadata, dict):
            raise ValueError("Expected a run metadata object.")
        report = make_report(args.trx, metadata)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(report, encoding="utf-8")
    except (OSError, ValueError, ET.ParseError) as error:
        # Do not echo private source text from parser exceptions.
        parser.exit(1, f"Could not generate report ({type(error).__name__}); review inputs privately.\n")
    print("Wrote redacted text summary. Retain the input evidence privately and review the summary before sharing.")


if __name__ == "__main__":
    main()
