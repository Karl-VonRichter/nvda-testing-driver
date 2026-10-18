import importlib.util
from pathlib import Path
import tempfile
import unittest


spec = importlib.util.spec_from_file_location("redact_test_report", Path(__file__).resolve().parents[1] / "redact_test_report.py")
report = importlib.util.module_from_spec(spec)
spec.loader.exec_module(report)


class RedactedReportTests(unittest.TestCase):
    run_id = "0123456789abcdef0123456789abcdef"

    def fixture(self, directory, summary=""):
        trx = Path(directory) / "private.trx"
        trx.write_text(f'''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <TestDefinitions><UnitTest id="1"><TestMethod name="ExampleMethod"/></UnitTest></TestDefinitions>
          <Results><UnitTestResult testId="1" outcome="Passed"><ResultFiles><ResultFile path="run-{self.run_id}.json"/></ResultFiles></UnitTestResult></Results>
          {summary}
        </TestRun>''', encoding="utf-8")
        return trx

    def test_omits_private_output_names_and_metadata_without_changing_outcomes(self):
        with tempfile.TemporaryDirectory() as directory:
            trx = Path(directory) / "private.trx"
            trx.write_text('''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <TestDefinitions><UnitTest id="1" name="Case (person@example.com)"><TestMethod name="NamedLandmarks"/></UnitTest></TestDefinitions>
              <Results><UnitTestResult testId="1" testName="person@example.com" outcome="NotExecuted">
                <Output><StdOut>person@example.com Other application speech secret-token</StdOut>
                <ErrorInfo><Message>Assert.Inconclusive failed. Private page contents</Message><StackTrace>C:\\Users\\Example</StackTrace></ErrorInfo></Output>
                <ResultFiles><ResultFile path="private-recording.wav"/><ResultFile path="run-0123456789abcdef0123456789abcdef.json"/></ResultFiles>
              </UnitTestResult></Results>
            </TestRun>''', encoding="utf-8")
            text = report.make_report(trx, {"RunId": self.run_id, "Url": "https://person@example.com/?token=secret-token", "Organisation": "Private organisation",
                "Browser": "chrome", "BrowserVersion": "140.0.7339.80", "NvdaVersion": "2025.3",
                "Revision": "abcdef1234567", "BuildTimestamp": "2026-10-07T02:00:00Z", "StartedUtc": "2026-10-07T03:00:00Z"})
            for private in ("person@example.com", "Other application", "secret-token", "Private page", "Users", "private-recording", "Private organisation"):
                self.assertNotIn(private, text)
            self.assertIn("Inconclusive: NamedLandmarks", text)
            self.assertIn("140.0.7339.80", text)
            self.assertIn("abcdef1234567", text)

    def test_rejects_empty_results(self):
        with tempfile.TemporaryDirectory() as directory:
            trx = Path(directory) / "empty.trx"
            trx.write_text(f'<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><ResultFiles><ResultFile path="run-{self.run_id}.json"/></ResultFiles></TestRun>', encoding="utf-8")
            with self.assertRaises(ValueError):
                report.make_report(trx, {"RunId": self.run_id})

    def test_refuses_metadata_from_a_different_run(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(ValueError):
                report.make_report(self.fixture(directory), {"RunId": "f" * 32})

    def test_refuses_unbound_or_ambiguous_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            trx = self.fixture(directory)
            content = trx.read_text(encoding="utf-8")
            for replacement in ("<ResultFiles/>", f'<ResultFiles><ResultFile path="run-{self.run_id}.json"/><ResultFile path="run-{"f" * 32}.json"/></ResultFiles>'):
                trx.write_text(content.replace(f'<ResultFiles><ResultFile path="run-{self.run_id}.json"/></ResultFiles>', replacement), encoding="utf-8")
                with self.assertRaises(ValueError):
                    report.make_report(trx, {"RunId": self.run_id})

    def test_preserves_run_abort_and_run_level_errors_without_private_details(self):
        with tempfile.TemporaryDirectory() as directory:
            trx = self.fixture(directory, '<ResultSummary outcome="Aborted"><RunInfos><RunInfo outcome="Error"><Text>person@example.com private application speech</Text></RunInfo></RunInfos></ResultSummary>')
            text = report.make_report(trx, {"RunId": self.run_id})
            self.assertIn("Run outcome: Aborted", text)
            self.assertIn("Run-level incidents: Error=1", text)
            self.assertIn("Passed: ExampleMethod", text)
            self.assertNotIn("person@example.com", text)
            self.assertNotIn("private application speech", text)

    def test_metadata_allowlist_rejects_emails_and_multiline_injection(self):
        self.assertEqual("[redacted]", report.safe_version("person@example.com"))
        self.assertEqual("[redacted]", report.safe_version("private-organisation-token"))
        self.assertEqual("[redacted]", report.safe_browser("private-profile-name"))
        self.assertEqual("[redacted]", report.safe_revision("main\nprivate speech"))
        self.assertEqual("[redacted]", report.safe_timestamp("2026-10-07\nprivate speech"))


if __name__ == "__main__":
    unittest.main()
