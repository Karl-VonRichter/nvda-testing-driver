"""Export reviewed source without Git history, ignored files, or build artifacts."""

import argparse
from pathlib import Path
import re
import subprocess
import zipfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path("artifacts/public-source.zip"))
    parser.add_argument("--forbid", action="append", default=[], help="Private text that must not appear in filenames or contents; repeat as needed.")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    paths = subprocess.check_output(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=root
    ).decode("utf-8").split("\0")
    forbidden = [value.casefold() for value in args.forbid]
    blocked_parts = {".git", ".vs", "bin", "obj", "artifacts", "testresults", "node_modules"}
    files = []
    issues = []
    for name in sorted(set(filter(None, paths))):
        path = root / name
        if not path.exists():  # Working-tree deletions.
            continue
        if path.is_symlink() or root not in path.resolve().parents:
            issues.append(f"{name}: links or paths outside the source tree cannot be exported")
            continue
        parts = {part.casefold() for part in Path(name).parts}
        if parts & blocked_parts:
            continue
        if path.name.casefold().startswith(".env") and path.name.casefold() != ".env.example":
            issues.append(f"{name}: local environment configuration")
            continue
        if re.fullmatch(r"run-[0-9a-f]{32}\.json", path.name, re.IGNORECASE):
            issues.append(f"{name}: private run metadata")
            continue
        if path.suffix.casefold() in {".pfx", ".p12", ".pem", ".key", ".runsettings", ".testsettings", ".log", ".trx", ".nupkg", ".snupkg"}:
            issues.append(f"{name}: private configuration, key, or generated output")
            continue
        data = path.read_bytes()
        # Check UTF-8 source and UTF-16 strings commonly found in Windows binaries.
        text = name + "\n" + data.decode("utf-8", errors="replace") + "\n" + data.decode("utf-16-le", errors="replace")
        if any(value in text.casefold() for value in forbidden):
            issues.append(f"{name}: forbidden private text")
        if re.search(r"[A-Za-z]:[\\/]Users[\\/][^\\/\s]+", text):
            issues.append(f"{name}: absolute workstation path")
        files.append((name, data))
    if issues:
        raise SystemExit("Source export refused:\n" + "\n".join(issues))
    output = args.output.resolve()
    if output == Path(__file__).resolve() or output in {root / name for name, _ in files}:
        raise SystemExit("The output must not overwrite source.")
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for name, data in files:
            archive.writestr(name, data)
    print(f"Exported {len(files)} source files to {output}")


if __name__ == "__main__":
    main()
