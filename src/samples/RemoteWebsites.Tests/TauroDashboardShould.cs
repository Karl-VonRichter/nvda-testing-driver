using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvdaTestingDriver;
using NvdaTestingDriver.Commands.NvdaCommands;
using NvdaTestingDriver.MSTest;
using NvdaTestingDriver.Selenium.Extensions;
using OpenQA.Selenium;

namespace RemoteWebsites.Tests
{
	/// <summary>
	/// Checks what NVDA announces on the signed-in Tauro dashboard.
	/// Each test asserts the accessible behaviour, so a failure is an accessibility bug; every test also writes
	/// what NVDA said to the test output.
	/// </summary>
	[TestClass]
	public class TauroDashboardShould
	{
		private const string DialogSelector = "[role=dialog], [role=alertdialog]";

		private const string OrgSwitcher = "button[aria-label^='Switch organisation']";

		private static int _escapesForNvdaModeSwitch;

		// Tier 1

		[TestMethod]
		public async Task AnnounceTheNewPageWhenFollowingASidebarLink()
		{
			await OpenAsync("/");
			await FocusAndReportAsync("nav[aria-label=Primary] a[href='/collars']");

			string text = await ListenAsync(() => TestHelper.NvdaDriver.SendKeysAsync(Key.Enter), TimeSpan.FromSeconds(6));
			Log("After activating the Collars link", text);
			Log("Focus after navigating", ActiveElement());

			Assert.IsTrue(new Uri(TestHelper.WebDriver.Url).AbsolutePath.StartsWith("/collars"), "The Collars link should navigate to /collars.");
			NvdaAssert.TextContains(text, "Collars");
		}

		[TestMethod]
		public async Task AnnounceANewApiKeyAndHandleItsRevokeDialog()
		{
			string keyName = "nvda-test-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
			var failures = new List<string>();
			await OpenAsync("/settings/api-keys");
			try
			{
				TestHelper.WebDriver.FindElement(By.Id("api-key-name")).SendKeys(keyName);
				await FocusAndReportAsync("form button[type=submit]");

				// Listen for longer than a toast normally stays up.
				string created = await ListenAsync(() => TestHelper.NvdaDriver.SendKeysAsync(Key.Enter), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(3));
				Log("After Create key (key values redacted)", Redact(created));
				Log("Focus after Create key", ActiveElement());
				if (string.IsNullOrWhiteSpace(created))
				{
					failures.Add("Nothing was announced after creating a key.");
				}

				if (IsDialogOpen())
				{
					Log("A dialog opened after Create key", Redact(TestHelper.WebDriver.FindElement(By.CssSelector(DialogSelector)).Text));
					await TestHelper.NvdaDriver.SendKeysAsync(Key.Escape);
					WaitUntil(() => !IsDialogOpen(), TimeSpan.FromSeconds(3));
				}

				// Revoke button naming: with several keys, "Revoke" alone doesn't say which key it revokes.
				IWebElement revokeButton = FindRevokeButtonFor(keyName);
				Log("Revoke buttons on the page", TestHelper.WebDriver.FindElements(By.XPath("//button[normalize-space()='Revoke']")).Count.ToString());
				TestHelper.WebDriver.Focus(revokeButton);
				Thread.Sleep(500);
				string revokeName = await TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(NavigatingSystemFocusCommands.ReportCurrentFocus);
				Log("Our key's Revoke button", revokeName);
				if (!NvdaTestHelper.TextContains(revokeName, keyName))
				{
					failures.Add($"The Revoke button doesn't name its key. NVDA said: \"{revokeName}\"");
				}

				string dialog = await ListenAsync(() => TestHelper.NvdaDriver.SendKeysAsync(Key.Enter), TimeSpan.FromSeconds(5));
				Log("After opening the revoke dialog", dialog);
				Assert.IsTrue(WaitUntil(IsDialogOpen, TimeSpan.FromSeconds(3)), "The Revoke button should open a confirmation dialog.");

				int presses = await PressEscapeUntilDialogClosesAsync(3);
				Log("Escape presses to close the revoke dialog", presses > 0 ? presses.ToString() : "did not close after 3");
				if (presses != 1)
				{
					failures.Add(presses == 0 ? "Escape does not close the revoke dialog." : $"The revoke dialog needed {presses} Escape presses to close.");
				}
			}
			finally
			{
				RevokeKey(keyName);
			}

			Assert.IsTrue(failures.Count == 0, string.Join(Environment.NewLine, failures));
		}

		[TestMethod]
		public async Task OpenTheCommandPaletteWithControlK()
		{
			await OpenAsync("/");
			string text = await ListenAsync(() => TestHelper.NvdaDriver.SendKeysAsync(Key.Control, Key.K), TimeSpan.FromSeconds(5));
			Log("After Ctrl+K", text);
			Log("Focus after Ctrl+K", ActiveElement());

			Assert.IsTrue(WaitUntil(IsDialogOpen, TimeSpan.FromSeconds(3)), $"Ctrl+K should open the command palette. NVDA said: \"{text}\"");
		}

		[TestMethod]
		public async Task GiveTheCommandPaletteARealName()
		{
			await OpenCommandPaletteAsync();
			string text = await TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(NavigatingSystemFocusCommands.ReportCurrentFocus);
			Log("Command palette focus", text);
			var dialog = TestHelper.WebDriver.FindElement(By.CssSelector(DialogSelector));
			Log("Dialog aria-label / aria-labelledby", $"{dialog.GetAttribute("aria-label")} / {dialog.GetAttribute("aria-labelledby")}");

			string spoken = await ListenAsync(() => TestHelper.NvdaDriver.SendCommandAsync(NavigatingSystemFocusCommands.ReportTitle), TimeSpan.FromSeconds(3));
			Assert.IsFalse(NvdaTestHelper.TextContains(text + " " + spoken, "dashboardSearch"), $"The palette is announced with an untranslated key. NVDA said: \"{text}\"");
		}

		[TestMethod]
		public async Task CloseTheCommandPaletteWithOneEscape()
		{
			await OpenCommandPaletteAsync();
			int presses = await PressEscapeUntilDialogClosesAsync(3);
			Log("Escape presses to close the command palette", presses > 0 ? presses.ToString() : "did not close after 3");

			// In focus mode NVDA keeps the first Escape for itself (it says "Browse mode") and only the next one reaches the page,
			// so two presses is normal there. Anything more means the page itself needs extra presses.
			Assert.IsTrue(presses > 0 && presses <= _escapesForNvdaModeSwitch + 1, $"The command palette took {presses} Escape presses ({_escapesForNvdaModeSwitch} used by NVDA to leave focus mode; 0 means it did not close).");
		}

		[TestMethod]
		public async Task LetKeyboardUsersMoveThroughTheMap()
		{
			await OpenAsync("/collars");
			await FocusAndReportAsync("[role=radiogroup]");

			bool enteredMap = false;
			bool leftMap = false;
			for (int i = 1; i <= 20 && !leftMap; i++)
			{
				string text = await ListenAsync(() => TestHelper.NvdaDriver.SendKeysAsync(Key.Tab), TimeSpan.FromSeconds(3));
				bool inMap = (bool)Js("return !!document.activeElement.closest('[role=application]');");
				Log($"Tab {i} ({(inMap ? "in map" : "outside map")}, {ActiveElement()})", text);
				enteredMap |= inMap;
				leftMap = enteredMap && !inMap;
			}

			Assert.IsTrue(enteredMap, "Tab should reach the map.");
			Assert.IsTrue(leftMap, "Tab should leave the map again within 20 presses.");
		}

		[TestMethod]
		public async Task ReachThePacketsPerHourChartWithTab()
		{
			await OpenAsync("/");

			// Start from the page heading; what NVDA says for the heading itself doesn't matter here.
			TestHelper.WebDriver.Focus(TestHelper.WebDriver.FindElement(By.CssSelector("main h1")));
			Thread.Sleep(500);

			for (int i = 1; i <= 40; i++)
			{
				string text = await ListenAsync(() => TestHelper.NvdaDriver.SendKeysAsync(Key.Tab), TimeSpan.FromSeconds(3));
				Log($"Tab {i} ({ActiveElement()})", text);
				if (NvdaTestHelper.TextContains(text, "Packets per hour"))
				{
					return;
				}
			}

			Assert.Fail("Tab never reached the Packets per hour chart in 40 presses.");
		}

		[TestMethod]
		public async Task AnnounceTheOrganisationSwitcherMenu()
		{
			await OpenAsync("/");
			string trigger = await FocusAndReportAsync(OrgSwitcher);
			Log("Org switcher", trigger);
			NvdaAssert.TextContains(trigger, "Switch organisation");

			string opened = await ListenAsync(() => TestHelper.NvdaDriver.SendKeysAsync(Key.Enter), TimeSpan.FromSeconds(4));
			Log("After opening the org switcher", opened);
			string next = await ListenAsync(() => TestHelper.NvdaDriver.SendKeysAsync(Key.DownArrow), TimeSpan.FromSeconds(3));
			Log("After Down arrow", next);
			string closed = await ListenAsync(() => TestHelper.NvdaDriver.SendKeysAsync(Key.Escape), TimeSpan.FromSeconds(3));
			Log("After Escape", closed);
			string focus = ActiveElement();
			Log("Focus after Escape", focus);

			NvdaAssert.TextContains(opened, "menu");
			Assert.IsTrue((bool)Js($"return document.activeElement.matches(\"{OrgSwitcher}\");"), $"Focus should return to the org switcher after Escape, but is on {focus}.");
		}

		// Tier 2

		[DataTestMethod]
		[DataRow("/collars")]
		[DataRow("/tags")]
		[DataRow("/gateways")]
		public async Task HaveOneLevelOneHeading(string path)
		{
			await OpenAsync(path);
			var headings = await QuickNavigateFromTopAsync(Key.D1, 4);
			Assert.IsTrue(headings.Count > 0, "NVDA found no level 1 heading at all.");
			int levelOne = headings.Count(h => NvdaTestHelper.TextContains(h, "heading level 1"));
			Assert.AreEqual(1, levelOne, $"Level 1 headings NVDA found: {string.Join(" / ", headings)}");
		}

		[DataTestMethod]
		[DataRow("/collars")]
		[DataRow("/tags")]
		[DataRow("/gateways")]
		public async Task NameEveryNavigationLandmark(string path)
		{
			await OpenAsync(path);
			var landmarks = await QuickNavigateFromTopAsync(Key.D, 12);
			Assert.IsTrue(landmarks.Count > 0, "NVDA found no landmarks at all.");
			var unnamed = landmarks.Where(l => l.Replace("\r", " ").Replace("\n", " ").Trim().StartsWith("navigation landmark", StringComparison.OrdinalIgnoreCase)).ToList();
			Assert.AreEqual(0, unnamed.Count, $"Landmarks NVDA found: {string.Join(" / ", landmarks)}");
		}

		[TestMethod]
		public async Task AnnounceTheToggleMapStateAndKeepFocus()
		{
			await OpenAsync("/collars");
			string button = await FocusAndReportAsync(By.XPath("//main//button[normalize-space()='Toggle Map']"));
			Log("Toggle Map", button);

			string text = await ListenAsync(() => TestHelper.NvdaDriver.SendKeysAsync(Key.Enter), TimeSpan.FromSeconds(4));
			string focus = ActiveElement();
			Log("After activating Toggle Map", text);
			Log("Focus after Toggle Map", focus);

			bool focusKept = (bool)Js("return document.activeElement.innerText.trim() === 'Toggle Map';");
			bool stateAnnounced = NvdaTestHelper.TextContains(button + " " + text, "pressed") || NvdaTestHelper.TextContains(button + " " + text, "expanded") || NvdaTestHelper.TextContains(button + " " + text, "collapsed");
			Assert.IsTrue(focusKept && stateAnnounced, $"Toggle Map should keep focus (kept: {focusKept}) and announce its state (announced: {stateAnnounced}). NVDA said: \"{button}\" then \"{text}\"");
		}

		[TestMethod]
		public async Task AnnounceColumnHeadersAndSortButtonsInTheCollarsTable()
		{
			await OpenAsync("/collars");
			string battery = await FocusAndReportAsync("button[aria-label='Sort Battery']");
			Log("Sort Battery", battery);
			NvdaAssert.TextContains(battery, "Sort Battery button");

			string next = await ListenAsync(() => TestHelper.NvdaDriver.SendCommandAsync(NavigatingSystemCaretCommands.MoveToNextColumn), TimeSpan.FromSeconds(3));
			Log("Ctrl+Alt+Right from Battery", next);
			NvdaAssert.TextContains(next, "SD Usage");
			NvdaAssert.TextContains(next, "column");
		}

		[DataTestMethod]
		[DataRow("/settings", "#profile-name", "Name")]
		[DataRow("/settings", "#profile-email", "Email")]
		[DataRow("/settings", "#profile-avatar", "Avatar")]
		[DataRow("/settings", "#profile-bio", "Bio")]
		[DataRow("/settings/api-keys", "#api-key-name", "Name")]
		[DataRow("/settings/security", "input[name=current]", "Current password")]
		[DataRow("/settings/security", "input[name=new]", "New password")]
		public async Task NameEverySettingsField(string path, string field, string expectedName)
		{
			await OpenAsync(path);
			string text = await FocusAndReportAsync(field);
			Log($"{path} {field}", text);
			NvdaAssert.TextContains(text, expectedName);
			Assert.IsTrue(NvdaTestHelper.TextContains(text, "edit"), $"Should be announced as an edit field. NVDA said: \"{text}\"");
		}

		// Tier 3

		[TestMethod]
		public void ShowWhetherMembersHaveARowMenu()
		{
			OpenAsync("/settings/members").GetAwaiter().GetResult();
			Thread.Sleep(2000);
			var rowMenus = TestHelper.WebDriver.FindElements(By.CssSelector("main tr button, main li button")).Where(b => b.Text.Trim() == "⋮" || b.GetAttribute("aria-haspopup") != null).ToList();
			Log("Member row menu buttons", rowMenus.Count.ToString());
			if (rowMenus.Count == 0)
			{
				Assert.Inconclusive("There is no row menu (⋮) on the members list to activate.");
			}
		}

		[TestMethod]
		public async Task ReadStatusBadgesAsWords()
		{
			await OpenAsync("/settings/members");
			Thread.Sleep(2000);
			var badge = TestHelper.WebDriver.FindElements(By.XPath("//main//*[not(*) and (normalize-space()='ADMIN' or normalize-space()='VIEW' or normalize-space()='Admin' or normalize-space()='View')]")).FirstOrDefault();
			if (badge == null)
			{
				Assert.Inconclusive("No ADMIN/VIEW badge on the members page.");
			}

			TestHelper.WebDriver.Focus(badge);
			Thread.Sleep(500);
			string text = await TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(NavigatingSystemFocusCommands.ReportCurrentFocus);
			Log($"Badge \"{badge.Text}\"", text);
			Assert.IsTrue(NvdaTestHelper.TextContains(text, badge.Text.Trim()), $"The badge should be read as a word. NVDA said: \"{text}\"");
		}

		private static async Task OpenAsync(string path)
		{
			TauroSession.EnsureSignedIn();
			TestHelper.WebDriver.Navigate().GoToUrl(TauroSession.BaseUrl + path);
			if (!WaitUntil(() => TestHelper.WebDriver.FindElements(By.CssSelector("main h1")).Count > 0, TimeSpan.FromSeconds(20)))
			{
				Assert.Fail($"{path} did not load (now at {TestHelper.WebDriver.Url}).");
			}

			// Let the page hydrate and fetch its data.
			Thread.Sleep(2500);
			TestHelper.WebDriverWrapper.SetBrowserWindowForeground();
			TestHelper.WebDriver.FocusOnWindow();
			await TestHelper.FocusPageContentAsync();
			await ListenAsync(() => Task.CompletedTask, TimeSpan.FromMilliseconds(500));
		}

		private static async Task OpenCommandPaletteAsync()
		{
			await OpenAsync("/");
			await ListenAsync(() => TestHelper.NvdaDriver.SendKeysAsync(Key.Control, Key.K), TimeSpan.FromSeconds(3));
			if (!WaitUntil(IsDialogOpen, TimeSpan.FromSeconds(2)))
			{
				// Fall back to the Search button, so the palette's own behaviour can still be checked.
				TestHelper.WebDriver.FindElement(By.XPath("//button[starts-with(normalize-space(), 'Search')]")).Click();
				Assert.IsTrue(WaitUntil(IsDialogOpen, TimeSpan.FromSeconds(3)), "The command palette did not open.");
			}

			Thread.Sleep(500);
		}

		private static Task<string> FocusAndReportAsync(string cssSelector) => FocusAndReportAsync(By.CssSelector(cssSelector));

		private static async Task<string> FocusAndReportAsync(By by)
		{
			TestHelper.WebDriver.Focus(TestHelper.WebDriver.FindElement(by));
			Thread.Sleep(500);
			return await TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(NavigatingSystemFocusCommands.ReportCurrentFocus);
		}

		/// <summary>
		/// Runs the action and returns everything NVDA says until it has been quiet for <paramref name="silence"/>, or "" if it says nothing.
		/// </summary>
		private static async Task<string> ListenAsync(Func<Task> action, TimeSpan timeout, TimeSpan? silence = null)
		{
			try
			{
				await TestHelper.NvdaDriver.StopReadingAsync();
			}
			catch (NvdaTestingDriver.Exceptions.TimeoutException)
			{
				// Nothing was being read.
			}

			try
			{
				return await TestHelper.NvdaDriver.GetNextSpokenMessageAsync(timeout, silence ?? TimeSpan.FromSeconds(1.5), action);
			}
			catch (NvdaTestingDriver.Exceptions.TimeoutException)
			{
				return string.Empty;
			}
		}

		/// <summary>
		/// Puts the browse mode cursor at the top of the page and presses a quick navigation key repeatedly.
		/// </summary>
		private static async Task<List<string>> QuickNavigateFromTopAsync(Key key, int maxPresses)
		{
			TestHelper.WebDriver.Focus(TestHelper.WebDriver.FindElement(By.CssSelector("main h1")));
			Thread.Sleep(500);
			await ListenAsync(() => TestHelper.NvdaDriver.SendKeysAsync(Key.Control, Key.Home), TimeSpan.FromSeconds(2));

			var found = new List<string>();
			for (int i = 0; i < maxPresses; i++)
			{
				string text = await ListenAsync(() => TestHelper.NvdaDriver.SendKeysAsync(key), TimeSpan.FromSeconds(3));
				Log($"Quick navigation {i + 1}", text);
				if (string.IsNullOrWhiteSpace(text) || NvdaTestHelper.TextContains(text, "no next"))
				{
					break;
				}

				found.Add(text.Replace("\r", string.Empty).Replace("\n", " ").Trim());
			}

			return found;
		}

		private static async Task<int> PressEscapeUntilDialogClosesAsync(int maxPresses)
		{
			_escapesForNvdaModeSwitch = 0;
			for (int i = 1; i <= maxPresses; i++)
			{
				string text = await ListenAsync(() => TestHelper.NvdaDriver.SendKeysAsync(Key.Escape), TimeSpan.FromSeconds(2));
				bool open = WaitUntil(() => !IsDialogOpen(), TimeSpan.FromSeconds(1)) == false;
				Log($"Escape {i} (dialog {(open ? "still open" : "closed")})", text);
				if (open && NvdaTestHelper.TextContains(text, "Browse mode"))
				{
					_escapesForNvdaModeSwitch++;
				}

				if (!open)
				{
					return i;
				}
			}

			return 0;
		}

		private static IWebElement FindRevokeButtonFor(string keyName)
		{
			// The nearest element holding both the key name and a Revoke button is its row; it must hold exactly one Revoke button.
			var row = TestHelper.WebDriver.FindElements(By.XPath($"//*[normalize-space(text())='{keyName}']/ancestor::*[.//button[normalize-space()='Revoke']][1]")).FirstOrDefault();
			Assert.IsNotNull(row, $"The new key {keyName} is not listed.");
			var buttons = row.FindElements(By.XPath(".//button[normalize-space()='Revoke']"));
			Assert.AreEqual(1, buttons.Count, $"Could not tell which Revoke button belongs to {keyName}.");
			return buttons[0];
		}

		/// <summary>
		/// Revokes the key this test created, and only that key.
		/// </summary>
		private static void RevokeKey(string keyName)
		{
			try
			{
				if (IsDialogOpen())
				{
					TestHelper.WebDriver.FindElement(By.CssSelector("body")).SendKeys(Keys.Escape);
					WaitUntil(() => !IsDialogOpen(), TimeSpan.FromSeconds(3));
				}

				TestHelper.WebDriver.Navigate().Refresh();
				if (!WaitUntil(() => TestHelper.WebDriver.FindElements(By.XPath($"//*[normalize-space(text())='{keyName}']")).Count > 0, TimeSpan.FromSeconds(10)))
				{
					Log("Cleanup", $"{keyName} is not listed, nothing to revoke.");
					return;
				}

				FindRevokeButtonFor(keyName).Click();
				Assert.IsTrue(WaitUntil(IsDialogOpen, TimeSpan.FromSeconds(3)), "The revoke dialog did not open during cleanup.");
				var dialog = TestHelper.WebDriver.FindElement(By.CssSelector(DialogSelector));
				var confirm = dialog.FindElements(By.XPath(".//button[contains(normalize-space(), 'Revoke')]")).LastOrDefault();
				Assert.IsNotNull(confirm, "The revoke dialog has no Revoke button.");
				confirm.Click();
				bool gone = WaitUntil(() => TestHelper.WebDriver.FindElements(By.XPath($"//*[normalize-space(text())='{keyName}']")).Count == 0, TimeSpan.FromSeconds(10));
				Log("Cleanup", gone ? $"Revoked {keyName}." : $"{keyName} is still listed: revoke it by hand.");
			}
			catch (Exception ex) when (!(ex is AssertFailedException))
			{
				Log("Cleanup failed", $"{ex.Message} Revoke {keyName} by hand.");
			}
		}

		private static bool IsDialogOpen() => TestHelper.WebDriver.FindElements(By.CssSelector(DialogSelector)).Any(d => d.Displayed);

		private static string ActiveElement() => (string)Js(
			"const e = document.activeElement; if (!e || e === document.body) return 'body';" +
			"return e.tagName.toLowerCase() + (e.getAttribute('role') ? '[role=' + e.getAttribute('role') + ']' : '') + ' \"' + (e.getAttribute('aria-label') || e.innerText || e.value || '').trim().slice(0, 40) + '\"';");

		private static object Js(string script) => ((IJavaScriptExecutor)TestHelper.WebDriver).ExecuteScript(script);

		private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
		{
			var deadline = DateTime.Now + timeout;
			do
			{
				try
				{
					if (condition())
					{
						return true;
					}
				}
				catch (WebDriverException)
				{
					// The page may be changing.
				}

				Thread.Sleep(200);
			}
			while (DateTime.Now < deadline);
			return false;
		}

		/// <summary>
		/// Hides anything that looks like an API key secret, so it doesn't end up in test logs.
		/// </summary>
		private static string Redact(string text) => System.Text.RegularExpressions.Regex.Replace(text ?? string.Empty, @"[A-Za-z0-9_\-]{24,}", "[redacted]");

		private static void Log(string label, string text) => Console.WriteLine($"[{label}] {text?.Replace("\r", string.Empty).Replace("\n", " | ")}");
	}
}
