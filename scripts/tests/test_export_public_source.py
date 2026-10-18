import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
import unittest
import zipfile


class PublicSourceExportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="nvda-export-test-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        (self.root / "scripts").mkdir()
        source = Path(__file__).resolve().parents[1] / "export_public_source.py"
        shutil.copyfile(source, self.root / "scripts/export_public_source.py")
        subprocess.run(["git", "init", "--quiet", str(self.root)], check=True)
        (self.root / ".gitignore").write_text("artifacts/\n.env\n", encoding="utf-8")
        (self.root / "example.cs").write_text("// Generic example\n", encoding="utf-8")

    def export(self, *arguments):
        return subprocess.run(
            [sys.executable, "scripts/export_public_source.py", *arguments],
            cwd=self.root, capture_output=True, text=True
        )

    def test_exports_current_source_without_history_ignored_data_or_deleted_files(self):
        (self.root / "deleted.cs").write_text("// Removed example", encoding="utf-8")
        subprocess.run(["git", "add", "."], cwd=self.root, check=True)
        (self.root / "deleted.cs").unlink()
        (self.root / "example.cs").write_text("// Current revision", encoding="utf-8")
        (self.root / ".env").write_text("EXAMPLE_PASSWORD=private", encoding="utf-8")
        (self.root / "artifacts").mkdir()
        (self.root / "artifacts/private.log").write_text("private output", encoding="utf-8")
        result = self.export()
        self.assertEqual(0, result.returncode, result.stderr)
        with zipfile.ZipFile(self.root / "artifacts/public-source.zip") as archive:
            self.assertNotIn("deleted.cs", archive.namelist())
            self.assertFalse(any(name.startswith((".git/", "artifacts/")) or name == ".env" for name in archive.namelist()))
            self.assertEqual(b"// Current revision", archive.read("example.cs"))

    def test_refuses_private_content(self):
        (self.root / "example.cs").write_text("// private-example-marker", encoding="utf-8")
        result = self.export("--forbid", "private-example-marker")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("forbidden private text", result.stderr)
        self.assertFalse((self.root / "artifacts/public-source.zip").exists())

    def test_refuses_tracked_environment_credentials(self):
        (self.root / ".env").write_text("EXAMPLE_PASSWORD=private", encoding="utf-8")
        subprocess.run(["git", "add", "--force", ".env"], cwd=self.root, check=True)
        result = self.export()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("local environment configuration", result.stderr)

    def test_refuses_uppercase_environment_credentials(self):
        (self.root / ".ENV").write_text("EXAMPLE_PASSWORD=private", encoding="utf-8")
        subprocess.run(["git", "add", "--force", ".ENV"], cwd=self.root, check=True)
        result = self.export()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("local environment configuration", result.stderr)

    def test_refuses_private_run_metadata_in_a_custom_directory(self):
        directory = self.root / "private-run-output"
        directory.mkdir()
        metadata = directory / "run-0123456789abcdef0123456789abcdef.json"
        metadata.write_text('{"Url":"https://example.org/private","Organisation":"Private example"}', encoding="utf-8")
        subprocess.run(["git", "add", "--force", str(metadata)], cwd=self.root, check=True)
        result = self.export()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("private run metadata", result.stderr)


if __name__ == "__main__":
    unittest.main()
