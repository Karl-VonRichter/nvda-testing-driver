# NVDA Testing Driver

[Leer en español](readme_es.md)

Automate accessibility tests with the NVDA screen reader on Windows. The driver starts NVDA, sends it real keyboard commands, and gives you the text NVDA speaks, so your tests can check the names, roles, states, and content that a screen reader user would hear.

It works for websites (with Selenium) and for desktop applications.

## What's supported

| | Supported |
| --- | --- |
| NVDA | 2025.1 or later, installed or at an explicit path (recommended). The bundled portable NVDA 2018.4.1 is used as a fallback. |
| Browsers | Chrome, Edge, and Chromium through Selenium. Firefox is untested: the window handling isn't browser-specific, but the samples don't start it. Internet Explorer is not supported. |
| Your test projects | Anything that can use .NET Standard 2.0, such as .NET 8 and later, or .NET Framework 4.7.2 and later. .NET 10 and .NET Framework 4.8 are tested. |
| Dependencies | Selenium.WebDriver 4.x and MSTest 3.x (for `NvdaTestingDriver.MSTest`). |
| Operating system | Windows, on an unlocked interactive desktop. Headless browsers and non-interactive build agents can't be used. |
| Last verified with | NVDA 2026.2 and Chrome 154 on Windows 11. |

Upgrading from 0.2.0-beta? See [CHANGELOG.md](CHANGELOG.md) for breaking changes.

## Packages

- `NvdaTestingDriver` starts NVDA, sends commands, and captures what it says.
- `NvdaTestingDriver.Selenium` finds the browser window for a WebDriver session, brings it to the foreground, and moves focus to page elements.
- `NvdaTestingDriver.MSTest` adds `NvdaAssert.TextContains`, which ignores differences in case, whitespace, and spoken hyphens.

The packages on NuGet are the earlier 0.2.0-beta release. Until a new release is published, reference the projects in `src/` directly.

## Your first test

This test opens the demo's components page in Chrome, focuses the email field, and checks what NVDA says about it. Start the demo first (see [Running the examples](#running-the-examples)).

```csharp
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvdaTestingDriver;
using NvdaTestingDriver.Commands.NvdaCommands;
using NvdaTestingDriver.MSTest;
using NvdaTestingDriver.Selenium;
using NvdaTestingDriver.Selenium.Extensions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

[TestClass]
[DoNotParallelize]
public class ComponentsPageShould
{
    private static readonly WebDriverWrapper Wrapper = new WebDriverWrapper();
    private static IWebDriver _browser;
    private static NvdaDriver _nvda;

    [ClassInitialize]
    public static async Task Start(TestContext context)
    {
        var options = new ChromeOptions();
        // Build Chrome's accessibility tree even if Chrome starts before NVDA.
        options.AddArgument("--force-renderer-accessibility");
        _browser = Wrapper.UpWebDriver(() => new ChromeDriver(options));

        _nvda = new NvdaDriver(nvda => nvda.BrowseModeSettings.AutoSayAllOnPageLoad = false);
        // Keys sent through NVDA are real keystrokes: only send them to the test browser.
        _nvda.BeforeSendingKeys = Wrapper.EnsureBrowserWindowForeground;
        await _nvda.ConnectAsync();
    }

    [ClassCleanup]
    public static async Task Stop()
    {
        await _nvda.DisconnectAsync();
        _browser.Quit();
    }

    [TestMethod]
    public async Task AnnounceTheEmailField()
    {
        _browser.Navigate().GoToUrl("http://127.0.0.1:5000/home/components");
        _browser.Focus(_browser.FindElement(By.Id("example-email")));

        string speech = await _nvda.SendCommandAndGetSpokenTextAsync(NavigatingSystemFocusCommands.ReportCurrentFocus);

        NvdaAssert.TextContains(speech, "Email");
        NvdaAssert.TextContains(speech, "edit");
        NvdaAssert.TextContains(speech, "required");
    }
}
```

`src/samples/TestSupport/SampleTestSession.cs` is a fuller version of this setup: it supports Edge and Chromium, hides Chrome's automation prompts, and moves focus back into the page when it ends up in the browser's toolbar.

## Finding out what NVDA says

Tests are easiest to write from what NVDA actually says. NVDA's Speech Viewer shows every announcement as text:

1. Start NVDA. Open the NVDA menu (NVDA+N), then choose Tools, Speech Viewer.
2. Open the page you want to test and move to the element the way a user would: Tab, or browse mode keys such as H for the next heading and F for the next form field. NVDA+Tab reports the element that has focus.
3. Copy the lines from Speech Viewer. For example, focusing the email field above gives something like `Email  edit  required  Enter an email address for this local example.  blank`.
4. In your test, reach the same element (with `Focus` or the same NVDA command) and assert the parts that matter: the name, role, and important states. Avoid asserting the whole line. Wording changes between NVDA versions, browsers, and languages.
5. Quit NVDA (NVDA+Q) before running the tests.

When a test runs, the driver closes any running NVDA and starts its own. It does not restart your NVDA afterwards. If you use another screen reader, turn it off during the run, since it will talk over the test and can move focus.

## Choosing NVDA

By default the driver uses an installed NVDA 2025.1 or later and connects through NVDA's built-in Remote Access. To control this:

- `NvdaExecutablePath` picks a specific NVDA 2025.1+ executable, installed or portable.
- `UseInstalledNvda = false` uses the bundled NVDA 2018.4.1 and its Remote add-on. It's kept for compatibility, but it reads modern web pages poorly, so use a current NVDA for website testing.

```csharp
var nvda = new NvdaDriver(options =>
{
    options.NvdaExecutablePath = @"C:\Tools\nvda\nvda.exe";
});
```

Each connection gets its own NVDA configuration folder under `%TEMP%\NvdaTestingDriver` and a random Remote Access key. Your own NVDA settings are never changed. The driver connects to NVDA on `127.0.0.1:6837` and uses no outside server. The test profile logs at INFO level (not DEBUG, which would record everything spoken) and turns off mouse tracking. The folder is deleted on disconnect; a crash or a locked file can leave one behind.

NVDA runs one instance at a time, so run screen-reader tests one at a time on a desktop you aren't using. `NvdaDriver.NvdaVersion` gives the version of the NVDA that was started.

## Sending commands and checking speech

Common NVDA commands are in `NvdaTestingDriver.Commands.NvdaCommands`, grouped as in the [NVDA user guide](https://www.nvaccess.org/files/nvda/documentation/userGuide.html). Desktop and laptop keyboard layouts are both supported.

- `SendCommandAndGetSpokenTextAsync` and `SendKeysAndGetSpokenTextAsync` send input and return what NVDA says, once it has been quiet for a moment.
- `SendCommandAsync` and `SendKeysAsync` send input without waiting for speech.
- `GetNextSpokenMessageAsync` starts listening, runs an action you pass in, and returns the speech that follows.
- `StopReadingAsync` stops NVDA speaking.

Use `NvdaAssert.TextContains` (or `NvdaTestHelper.TextContains`, which returns a bool) to compare speech.

For websites, set `BeforeSendingKeys` to `WebDriverWrapper.EnsureBrowserWindowForeground`, as in the example above. Keys go to whichever window is in the foreground, so the guard refuses to send them if the test browser can't be brought forward. `webDriver.Focus(element)` focuses an element and only adds `tabindex="-1"` when the element can't take focus otherwise.

## Running the examples

`src/samples/AccessibleDemo` is a small local site with a contact page, a WCAG tree view, and a components page with a required field, a validation alert, a dialog, and a table. To browse it yourself:

```powershell
dotnet run --project src/samples/AccessibleDemo --no-launch-profile --urls http://127.0.0.1:5000
```

`AccessibleDemo.Core.Tests` starts the built demo on a free local port, runs NVDA tests against it, and stops it afterwards. Screen-reader tests only run when you ask for them:

```powershell
$env:NVDA_TEST_RUN_INTERACTIVE = 'true'
dotnet test src/samples/AccessibleDemo.Core.Tests/AccessibleDemo.Core.Tests.csproj --configuration Release
Remove-Item Env:NVDA_TEST_RUN_INTERACTIVE
```

| Variable | Meaning |
| --- | --- |
| `NVDA_TEST_RUN_INTERACTIVE` | Set to `true` to run the screen-reader samples. Otherwise they are skipped. |
| `NVDA_TEST_BROWSER` | `chrome` (default), `edge`, or `chromium` |
| `NVDA_TEST_BROWSER_BINARY` | Path to the browser executable, if not the default |
| `NVDA_TEST_NVDA_EXECUTABLE` | Path to an NVDA 2025.1+ executable |
| `NVDA_TEST_USE_INSTALLED` | `false` to use the bundled NVDA 2018.4.1 |

Selenium Manager finds the browser driver. For `chromium` without a binary path, it downloads stable Chrome for Testing.

`RemoteWebsites.Tests` runs similar checks against a page you configure: an element's announcement, named landmarks, and closing a dialog with Escape. It has no built-in website. Only test sites you are allowed to test. Setup, and the private run records the samples write, are described in [docs/evidence-and-reporting.md](docs/evidence-and-reporting.md).

## Building and testing this repository

You need the .NET 10 SDK or later on Windows.

```powershell
dotnet build src/NvdaTestingDriver.sln --configuration Release
dotnet test src/NvdaTestingDriver.sln --configuration Release --no-build
```

This runs the driver regression tests and a .NET Framework 4.8 compatibility test. They start neither NVDA nor a browser, so they run anywhere, including CI. The screen-reader samples are skipped unless `NVDA_TEST_RUN_INTERACTIVE` is `true`.

GitHub Actions runs the build and these tests on a hosted Windows runner for pushes and pull requests to `master`, `main`, and `develop`. A manually triggered job runs the local screen-reader samples on a self-hosted runner labelled `nvda-interactive`, which needs an unlocked desktop, a browser, and NVDA. Pull requests never run on that machine.

## License and acknowledgements

NVDA Testing Driver was created by Juan José Montiel and is licensed under the GNU General Public License v3; see [license.txt](license.txt).

This project would not have been possible without all the people who have helped me with some aspect of it:

- To [Pablo Núñez](https://www.twitter.com/pablonete), who in a Twitter conversation gave me the idea of doing screen reader tests, and was the seed of this project. Hopefully you can use it in your developments! :)
- To [Christopher Toth](https://www.twitter.com/mongoose_q) and Tyler Spivey, who developed the wonderful complement for NVDA [NvdaRemote](https://www.nvdaremote.com), and that has allowed me to control NVDA remotely.
- To [Jose Manuel Delicado](https://www.twitter.com/jmdaweb), who helped me to understand a little better the NvdaRemote plugin and to modify it to be able to debug it.
- And last but not least, to my wife, [Núria](https://www.twitter.com/amaterasu_n), who puts up with my hours sitting in front of the computer, and takes care of everything during those hours in which I am immersed in this world of codes. Thank you, my love!
