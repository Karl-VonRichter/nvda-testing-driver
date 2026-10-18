# Changelog

## Unreleased

Changes since 0.2.0-beta, the last release on NuGet.

### Breaking changes

- **Selenium 4 and MSTest 3.** `NvdaTestingDriver.Selenium` needs Selenium.WebDriver 4.x and `NvdaTestingDriver.MSTest` needs MSTest 3.x. Update these together in your test projects.
- **An installed NVDA is used by default.** If NVDA 2025.1 or later is installed, the driver starts it instead of the bundled 2018.4.1 copy. Set `UseInstalledNvda = false` to keep the old behaviour. Speech can differ between NVDA versions, so expected text in existing tests may need updating.
- **`WebDriverWrapper.UpWebDriver` throws when it can't find the browser window.** It now finds the window by giving the page a temporary unique title, and throws `InvalidOperationException` if no visible window appears within 10 seconds. It throws `NotSupportedException` outside Windows. It no longer checks the driver type, so any local browser driver that runs JavaScript can be used.
- **`SetBrowserWindowForeground` no longer switches the browser to full screen.** Chrome announces how to leave full screen, and NVDA read it. The window is still maximised.
- **`Focus(element)` throws when the element can't take focus.** It also no longer adds `tabindex="-1"` to elements that are already focusable, which used to take them out of the tab order.
- **`TextContains` throws `ArgumentNullException` for null text** instead of failing inside the comparison.
- **`GetNextSpokenMessageAsync` waits for silence after the latest message.** The quiet period used to start from the first message, which cut off long announcements. Captures can now take slightly longer and contain more text.
- **`NavigatingSystemCaretCommands.MoveToPreviousColumn` sends Control+Alt+Left Arrow.** It used to send Right Arrow, the same as `MoveToNextColumn`.
- **`NvdaTestingDriver.xml` is no longer in the repository.** The documentation file is still generated at build time and included in the package.

### Added

- `NvdaDriverOptions.UseInstalledNvda` and `NvdaDriverOptions.NvdaExecutablePath` to choose which NVDA runs.
- `NvdaDriver.BeforeSendingKeys`, run before every keystroke. Set it to `WebDriverWrapper.EnsureBrowserWindowForeground` so keys only go to the test browser.
- `WebDriverWrapper.EnsureBrowserWindowForeground` and `IsBrowserWindowForeground`.
- `NvdaDriver.NvdaVersion`, the product version of the NVDA that was started.
- Support for Edge and Chromium as well as Chrome in the samples.
- Driver regression tests and a .NET Framework 4.8 compatibility test that run without NVDA or a browser.
- A components page in the demo app (required field, validation alert, dialog, table) and tests for it.
- A configurable website sample, private run records, and scripts to redact test reports and export source. See [docs/evidence-and-reporting.md](docs/evidence-and-reporting.md).

### Changed

- Each connection uses its own temporary NVDA profile and a random Remote Access key, both deleted on disconnect. Your own NVDA settings are never changed.
- The test profile logs at INFO instead of DEBUG and turns off mouse tracking.
- The Remote TLS handshake times out after 10 seconds. A failed connection cleans up before rethrowing.
- Disconnecting only stops NVDA processes the driver started.
- When NVDA doesn't confirm a speech cancel, commands carry on instead of failing.
- The demo app, samples, and tests target .NET 10, and the demo uses Bootstrap 5.
- CI runs on hosted Windows runners, with an optional manual job for screen-reader tests on a self-hosted desktop.

### Fixed

- `UpWebDriver` no longer crashes when the browser process has no window, or picks up the user's own browser windows.
- The laptop keyboard layout fallback, which checked the desktop layout's list.
- Remote messages split across reads, or several in one read, are now parsed correctly.
