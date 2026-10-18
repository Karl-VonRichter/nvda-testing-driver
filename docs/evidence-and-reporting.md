# Testing a configured website, evidence, and reports

This guide covers `RemoteWebsites.Tests`, the private run records written by the samples, and the scripts for sharing results and publishing source. You don't need any of it to use the driver in your own tests.

## Testing a configured website

`RemoteWebsites.Tests` has no built-in URL, credentials, sign-in, cookie handling, or anything that creates data. Everything comes from environment variables, and each test is inconclusive until it is configured. Only test sites you are allowed to test, and keep your configuration out of public source control.

To try it against the local demo, start the demo in one terminal:

```powershell
dotnet run --project src/samples/AccessibleDemo --no-launch-profile --urls http://127.0.0.1:5000
```

Then in another:

```powershell
$env:NVDA_TEST_RUN_INTERACTIVE = 'true'
$env:NVDA_TEST_PAGE_URL = 'http://127.0.0.1:5000/home/components'
$env:NVDA_TEST_ELEMENT_SELECTOR = '#example-email'
$env:NVDA_TEST_EXPECTED_TEXT = 'Email'
$env:NVDA_TEST_PAGE_TITLE = 'Accessible components - Accessible Demo'
# The exact NVDA+T announcement for your browser and NVDA language and version.
$env:NVDA_TEST_WINDOW_ANNOUNCEMENT = 'Accessible components - Accessible Demo - Google Chrome'
$env:NVDA_TEST_VERSION_TOGGLE_SELECTOR = '#example-version-toggle'
$env:NVDA_TEST_REVISION_SELECTOR = '#example-revision'
$env:NVDA_TEST_BUILD_TIMESTAMP_SELECTOR = '#example-build-timestamp'
$env:NVDA_TEST_ORGANISATION_SELECTOR = '#example-organisation'
dotnet test src/samples/RemoteWebsites.Tests/RemoteWebsites.Tests.csproj --configuration Release
Remove-Item Env:NVDA_TEST_RUN_INTERACTIVE
```

The browser and NVDA variables from the [readme](../readme.md#running-the-examples) apply here too.

### Settings

| Variable | Meaning |
| --- | --- |
| `NVDA_TEST_PAGE_URL` | The page to open |
| `NVDA_TEST_ELEMENT_SELECTOR` | CSS selector for the element to focus |
| `NVDA_TEST_EXPECTED_TEXT` | Text NVDA must say when reporting that element |
| `NVDA_TEST_PAGE_TITLE` | The page's exact title |
| `NVDA_TEST_WINDOW_ANNOUNCEMENT` | What NVDA says for NVDA+T (report window title) on this page |
| `NVDA_TEST_REVISION_SELECTOR`, `NVDA_TEST_BUILD_TIMESTAMP_SELECTOR`, `NVDA_TEST_ORGANISATION_SELECTOR` | Elements on the page showing which deployment is being tested. All three are required. |
| `NVDA_TEST_VERSION_TOGGLE_SELECTOR` | Optional control to click to show those elements, such as a `<summary>` |
| `NVDA_TEST_LANDMARKS` | JSON list of landmarks to check, below |
| `NVDA_TEST_OPEN_DIALOG_SELECTOR`, `NVDA_TEST_DIALOG_SELECTOR`, `NVDA_TEST_EXPECTED_ESCAPES` | Dialog to check, below |

The deployment values are read from the page, not from your checkout, so a run records what was actually deployed. A run without them is inconclusive.

### What the tests check

`AnnounceTheConfiguredElement` focuses the element and checks NVDA's focus report contains the expected text.

`AnnounceTheConfiguredNamedLandmarks` moves through landmarks with NVDA's D key. Set `NVDA_TEST_LANDMARKS` to a JSON array in navigation order:

```json
[{"Selector": "nav[aria-label]", "Name": "Main navigation", "Announcement": "Main navigation navigation landmark"}]
```

For each landmark, the browser's computed accessible name and role must match. Native header, footer, and section landmarks count. NVDA's announcement must start with the name and the spoken role (`<role> landmark` in English; set `RoleAnnouncement` for other languages) and match `Announcement` completely, including any speech for content inside the landmark. Record the full announcement on the NVDA and browser versions you support. The word "landmark" alone isn't enough.

`DismissTheConfiguredDialogWithObservedEscapes` focuses the opener, presses Enter, and then presses Escape until the dialog is no longer visible, checking after each press. `NVDA_TEST_EXPECTED_ESCAPES` is `1` or `2`: some dialogs need a second Escape because NVDA uses the first to leave focus mode. The dialog selector must only match the open dialog. NVDA saying "Browse mode" doesn't prove the dialog closed. The local `ComponentsShould` sample separately checks that one Escape closes its dialog in the DOM and returns focus.

### How captures are guarded

NVDA's speech doesn't say which application spoke, so a notification from another program could be mistaken for the page. `WebsiteEvidence` wraps every capture:

- Before and after, it checks the exact URL (including hash routes), the page title, that the page has focus, and NVDA's window announcement.
- During the capture, it checks the foreground window every 50 ms. If another window takes the foreground at any point, the result is inconclusive. It doesn't bring the browser back, because that would hide the problem.
- For landmark captures, the speech must match the expected announcement exactly. Extra or unexpected speech makes the result inconclusive instead of passing.

These checks are a guard, not proof of where speech came from.

## Private run records

Every interactive sample session writes `run-<id>.json`, where the id is 32 hex digits, and attaches it to each test result. It records:

- when the run started
- the browser name and version reported by WebDriver
- NVDA's product version
- the URL, organisation, deployment revision, and build timestamp

Values that couldn't be read are `unavailable`, including when startup fails. Records go to `artifacts/private`, or the folder in `NVDA_TEST_PRIVATE_ARTIFACTS`. Both should be ignored by Git, and `run-*.json` names are ignored everywhere.

The local demo embeds its source revision and UTC build time in its assembly and shows them under Version in its footer. Pass `-p:DeploymentRevision=<revision> -p:BuildTimestampUtc=<ISO timestamp>` when building to set them yourself. The local sample reads these from the built demo assembly.

## Sharing results

Raw TRX files can contain private page content, other applications' speech, and profile email addresses. Keep them private and share a redacted summary instead:

```powershell
dotnet test src/samples/RemoteWebsites.Tests/RemoteWebsites.Tests.csproj --configuration Release --results-directory artifacts/private --logger 'trx;LogFileName=run.trx'
# Use the run JSON attached to this TRX, not one from another run.
python scripts/redact_test_report.py artifacts/private/run.trx --metadata artifacts/private/run-<id>.json --output artifacts/shared-report.txt
```

The script refuses to run unless the JSON's run ID matches the TRX. The summary contains test names, outcomes (including failed, skipped, inconclusive, and aborted runs), and checked browser, NVDA, revision, and timestamp values. It leaves out all speech, error text, stack traces, attachments, URLs, and organisations. Read it before sharing.

## Publishing source

Deleting a private file doesn't remove it from Git history. If history has ever held private material, publish a reviewed export to a new repository rather than pushing the repository itself:

```powershell
python scripts/export_public_source.py --output artifacts/public-source.zip --forbid example-private-domain.com
```

The export contains the current tree and no history. It leaves out ignored files, build output, test results, `.env` files, keys, and private run records. It stops if a file contains an absolute workstation path or any `--forbid` text (repeat the option as needed). It doesn't look inside nested archives, such as the bundled NVDA zip, and it isn't a complete secret scanner, so review the archive before publishing.
