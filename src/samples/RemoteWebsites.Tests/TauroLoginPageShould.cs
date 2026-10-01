using System;
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
	/// Checks what NVDA announces on the Tauro dashboard login page.
	/// Expected texts come from NVDA 2026.2 with Chrome; NvdaAssert.TextContains ignores line breaks and extra spaces.
	/// When reporting focus, NVDA says "focused" between the role and the state, so those are checked separately.
	/// </summary>
	[TestClass]
	public class TauroLoginPageShould
	{
		private const string LoginUrl = "https://dashboard.tauro-research.com/login";

		private const string EmailField = "input[name=email]";

		private const string PasswordField = "input[name=password]";

		private const string SubmitButton = "button[type=submit]";

		[TestInitialize]
		public async Task OpenLoginPage()
		{
			TauroSession.EnsureSignedOut();
			TestHelper.WebDriver.Navigate().GoToUrl(LoginUrl);
			WaitForElement(EmailField);
			TestHelper.WebDriverWrapper.SetBrowserWindowForeground();
			TestHelper.WebDriver.FocusOnWindow();
			await TestHelper.FocusPageContentAsync();
		}

		[TestMethod]
		public async Task AnnounceThePageTitle()
		{
			// A notification from another app can briefly become the foreground window, and NVDA then reads its title instead.
			string text = string.Empty;
			for (int attempt = 0; attempt < 3 && !NvdaTestHelper.TextContains(text, "Google Chrome"); attempt++)
			{
				TestHelper.WebDriverWrapper.EnsureBrowserWindowForeground();
				text = await TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(NavigatingSystemFocusCommands.ReportTitle);
			}

			NvdaAssert.TextContains(text, "Login Tauro Dashboard");
		}

		[TestMethod]
		public async Task AnnounceTheMainHeading()
		{
			string text = await FocusAndReportAsync("h1");
			NvdaAssert.TextContains(text, "Login heading");
			NvdaAssert.TextContains(text, "level 1");
		}

		[TestMethod]
		public async Task AnnounceTheSkipLink()
		{
			string text = await FocusAndReportAsync("a[href='#main-content']");
			NvdaAssert.TextContains(text, "Skip to main content");
			NvdaAssert.TextContains(text, "link");
		}

		[TestMethod]
		public async Task AnnounceTheEmailFieldWithItsLabelAndPlaceholder()
		{
			string text = await FocusAndReportAsync(EmailField);
			NvdaAssert.TextContains(text, "Email");
			NvdaAssert.TextContains(text, "edit");
			NvdaAssert.TextContains(text, "Enter your email");
		}

		[TestMethod]
		public async Task AnnounceTheEmailFieldAsRequired()
		{
			// Known issue: the label's asterisk is read as "star", but the input has no required/aria-required,
			// so NVDA never says "required". Fix: add required to the input and aria-hidden="true" to the asterisk.
			string text = await FocusAndReportAsync(EmailField);
			NvdaAssert.TextContains(text, "required");
		}

		[TestMethod]
		public async Task AnnounceThePasswordFieldAsRequired()
		{
			string text = await FocusAndReportAsync(PasswordField);
			Console.WriteLine($"NVDA said for the password field: \"{text}\"");
			Assert.IsFalse(NvdaTestHelper.TextContains(text, "star"), $"The password label's asterisk should be hidden from screen readers. NVDA said: \"{text}\"");
			NvdaAssert.TextContains(text, "required");
		}

		[TestMethod]
		public async Task AnnounceErrorsWhenSubmittingAnEmptyRegistrationForm()
		{
			// Every field is left empty, so only the client-side validation runs and no account can be created.
			TestHelper.WebDriver.Navigate().GoToUrl(TauroSession.BaseUrl + "/register");
			WaitForElement("form " + SubmitButton);
			TestHelper.WebDriverWrapper.SetBrowserWindowForeground();
			var emptyFields = TestHelper.WebDriver.FindElements(By.CssSelector("form input:not([type=hidden]):not([aria-hidden=true])"));
			Assert.IsTrue(emptyFields.All(f => string.IsNullOrEmpty(f.GetAttribute("value"))), "The registration form should start empty.");

			await FocusAndReportAsync("form " + SubmitButton);
			string text = await PressEnterAndListenAsync(TimeSpan.FromSeconds(6));
			Console.WriteLine($"NVDA said after an empty registration: \"{text}\"");

			Assert.IsTrue(new Uri(TestHelper.WebDriver.Url).AbsolutePath.StartsWith("/register"), "An empty registration should stay on the registration page.");
			NvdaAssert.TextContains(text, "invalid entry");
		}

		[TestMethod]
		public async Task AnnounceThePasswordFieldAsProtected()
		{
			string text = await FocusAndReportAsync(PasswordField);
			NvdaAssert.TextContains(text, "Password edit");
			NvdaAssert.TextContains(text, "protected");
			NvdaAssert.TextContains(text, "Enter your password");
		}

		[TestMethod]
		public async Task AnnounceTheShowPasswordToggleAndItsState()
		{
			string text = await FocusAndReportAsync("button[aria-controls][aria-pressed]");
			NvdaAssert.TextContains(text, "Show password toggle button");
			NvdaAssert.TextContains(text, "not pressed");

			text = await TestHelper.NvdaDriver.SendKeysAndGetSpokenTextAsync(Key.Space);
			NvdaAssert.TextContains(text, "pressed");
			Assert.IsFalse(NvdaTestHelper.TextContains(text, "not pressed"), $"NVDA should announce the toggle as pressed. NVDA said: \"{text}\"");
		}

		[TestMethod]
		public async Task AnnounceTheRememberMeCheckboxAndItsState()
		{
			string text = await FocusAndReportAsync("button[role=checkbox]");
			NvdaAssert.TextContains(text, "Remember me check box");
			NvdaAssert.TextContains(text, "not checked");

			text = await TestHelper.NvdaDriver.SendKeysAndGetSpokenTextAsync(Key.Space);
			NvdaAssert.TextContains(text, "checked");
			Assert.IsFalse(NvdaTestHelper.TextContains(text, "not checked"), $"NVDA should announce the checkbox as checked. NVDA said: \"{text}\"");
		}

		[TestMethod]
		public async Task AnnounceTheSubmitButton()
		{
			string text = await FocusAndReportAsync(SubmitButton);
			NvdaAssert.TextContains(text, "Continue button");
		}

		[TestMethod]
		public async Task MoveThroughTheFormInALogicalTabOrder()
		{
			await FocusAndReportAsync(EmailField);

			string[] expectedStops =
			{
				"Password edit",
				"Show password toggle button",
				"Remember me check box",
				"Continue button",
				"Forgot your password?",
				"Create an account",
			};

			foreach (var expected in expectedStops)
			{
				string text = await TestHelper.NvdaDriver.SendKeysAndGetSpokenTextAsync(Key.Tab);
				NvdaAssert.TextContains(text, expected);
			}
		}

		[TestMethod]
		public async Task AnnounceTheEmailErrorWhenSubmittingAnEmptyForm()
		{
			await FocusAndReportAsync(SubmitButton);
			string text = await PressEnterAndListenAsync(TimeSpan.FromSeconds(6));

			// The form moves focus to the first invalid field, and NVDA reads its error message.
			NvdaAssert.TextContains(text, "Email");
			NvdaAssert.TextContains(text, "invalid entry");
			NvdaAssert.TextContains(text, "Enter a valid email address");
		}

		[TestMethod]
		public async Task AnnounceThePasswordErrorWhenSubmittingAnEmptyForm()
		{
			await FocusAndReportAsync(SubmitButton);
			await PressEnterAndListenAsync(TimeSpan.FromSeconds(6));

			string text = await FocusAndReportAsync(PasswordField);
			NvdaAssert.TextContains(text, "invalid entry");
			NvdaAssert.TextContains(text, "Enter your password");
		}

		[TestMethod]
		public async Task AnnounceAnErrorWhenTheCredentialsAreWrong()
		{
			TestHelper.WebDriver.FindElement(By.CssSelector(EmailField)).SendKeys("nvda-test@example.com");
			TestHelper.WebDriver.FindElement(By.CssSelector(PasswordField)).SendKeys("not-a-real-password");
			await FocusAndReportAsync(SubmitButton);

			string text = await PressEnterAndListenAsync(TimeSpan.FromSeconds(10));
			Console.WriteLine($"NVDA said after a wrong login: \"{text}\"");

			// The page shows the error in its role="alert" paragraph; NVDA should read that out without the user moving focus.
			string alertText = TestHelper.WebDriver.FindElement(By.CssSelector("form [role=alert]")).Text;
			Assert.IsFalse(string.IsNullOrWhiteSpace(alertText), $"The page should show an error message. NVDA said: \"{text}\"");
			NvdaAssert.TextContains(text, alertText);
		}

		private static async Task<string> FocusAndReportAsync(string cssSelector)
		{
			TestHelper.WebDriver.Focus(TestHelper.WebDriver.FindElement(By.CssSelector(cssSelector)));
			Thread.Sleep(500);
			return await TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(NavigatingSystemFocusCommands.ReportCurrentFocus);
		}

		private static async Task<string> PressEnterAndListenAsync(TimeSpan timeout)
		{
			await TestHelper.NvdaDriver.StopReadingAsync();

			// Wait for 2 seconds of silence, so announcements that follow a server round trip are included.
			return await TestHelper.NvdaDriver.GetNextSpokenMessageAsync(timeout, TimeSpan.FromSeconds(2), () => TestHelper.NvdaDriver.SendKeysAsync(Key.Enter));
		}

		private static void WaitForElement(string cssSelector)
		{
			var deadline = DateTime.Now.AddSeconds(20);
			while (TestHelper.WebDriver.FindElements(By.CssSelector(cssSelector)).Count == 0)
			{
				if (DateTime.Now > deadline)
				{
					throw new TimeoutException($"{cssSelector} did not appear on {LoginUrl}.");
				}

				Thread.Sleep(250);
			}

			// Let the page finish hydrating, so focus and state changes are handled by the app.
			Thread.Sleep(1000);
		}
	}
}
