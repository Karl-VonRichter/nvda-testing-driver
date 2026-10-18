using System;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvdaTestingDriver.Commands.NvdaCommands;
using NvdaTestingDriver.MSTest;
using NvdaTestingDriver.Selenium.Extensions;
using OpenQA.Selenium;
using SampleTests;

namespace RemoteWebsites.Tests
{
	[TestClass]
	[DoNotParallelize]
	public class ConfiguredWebsiteShould
	{
		public TestContext TestContext { get; set; }

		[TestCleanup]
		public void AttachPrivateMetadata() => TestContext.AddResultFile(TestHelper.Session.Metadata.FilePath);

		[TestInitialize]
		public async Task OpenPage()
		{
			TestHelper.WebDriver.Navigate().GoToUrl(TestHelper.PageUrl.AbsoluteUri);
			await TestHelper.Session.FocusPageContentAsync();
		}

		[TestMethod]
		public async Task AnnounceTheConfiguredElement()
		{
			TestHelper.WebDriver.Focus(TestHelper.WebDriver.FindElement(By.CssSelector(TestHelper.ElementSelector)));
			await Task.Delay(500);
			string text = await TestHelper.Evidence.CaptureAsync(() => TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(NavigatingSystemFocusCommands.ReportCurrentFocus));
			NvdaAssert.TextContains(text, TestHelper.ExpectedText);
		}

		[TestMethod]
		public async Task AnnounceTheConfiguredNamedLandmarks()
		{
			string configuration = Environment.GetEnvironmentVariable("NVDA_TEST_LANDMARKS");
			if (string.IsNullOrWhiteSpace(configuration)) Assert.Inconclusive("Configure named landmarks to run this example.");
			var landmarks = JsonSerializer.Deserialize<Landmark[]>(configuration);
			if (landmarks == null || landmarks.Length == 0) Assert.Inconclusive("At least one named landmark is required.");
			await TestHelper.NvdaDriver.SendKeysAsync(NvdaTestingDriver.Key.Control, NvdaTestingDriver.Key.Home);
			foreach (var landmark in landmarks)
			{
				if (string.IsNullOrWhiteSpace(landmark.Selector) || string.IsNullOrWhiteSpace(landmark.Name) || string.IsNullOrWhiteSpace(landmark.Announcement))
					Assert.Inconclusive("Each landmark needs a selector, accessible name, and exact expected announcement.");
				var element = TestHelper.WebDriver.FindElements(By.CssSelector(landmark.Selector)).FirstOrDefault();
				if (element == null) Assert.Inconclusive("The configured named landmark is absent from the expected website.");
				var accessibleElement = element as WebElement;
				if (accessibleElement == null)
					Assert.Inconclusive("The browser driver must support computed accessible landmark names and roles.");
				string role = accessibleElement.ComputedAccessibleRole;
				if (!element.Displayed || !string.Equals(accessibleElement.ComputedAccessibleLabel, landmark.Name, StringComparison.Ordinal) ||
					!Array.Exists(new[] { "banner", "complementary", "contentinfo", "form", "main", "navigation", "region", "search" }, value => role == value))
					Assert.Inconclusive("The configured named landmark is not present in the expected website context.");
				WebsiteEvidence.RequireNamedLandmarkAnnouncement(landmark.Name, landmark.RoleAnnouncement ?? role + " landmark", landmark.Announcement);
				await TestHelper.Evidence.CaptureAsync(() => TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(BrowseModeCommands.NextLandmark), landmark.Announcement);
			}
		}

		public sealed class Landmark
		{
			public string Selector { get; set; }
			public string Name { get; set; }
			public string Announcement { get; set; }
			public string RoleAnnouncement { get; set; }
		}

		[TestMethod]
		public async Task DismissTheConfiguredDialogWithObservedEscapes()
		{
			string openerSelector = Environment.GetEnvironmentVariable("NVDA_TEST_OPEN_DIALOG_SELECTOR");
			string dialogSelector = Environment.GetEnvironmentVariable("NVDA_TEST_DIALOG_SELECTOR");
			int expected = 0;
			if (string.IsNullOrWhiteSpace(openerSelector) || string.IsNullOrWhiteSpace(dialogSelector) ||
				!int.TryParse(Environment.GetEnvironmentVariable("NVDA_TEST_EXPECTED_ESCAPES"), out expected) || expected < 1 || expected > 2)
				Assert.Inconclusive("Configure the dialog opener, dialog, and expected Escape count (1 or 2) to run this example.");
			TestHelper.WebDriver.Focus(TestHelper.WebDriver.FindElement(By.CssSelector(openerSelector)));
			await Task.Delay(500);
			await TestHelper.Evidence.CaptureAsync(() => TestHelper.NvdaDriver.SendKeysAndGetSpokenTextAsync(NvdaTestingDriver.Key.Enter));
			int attempt = 0;
			bool IsOpen() => TestHelper.WebDriver.FindElements(By.CssSelector(dialogSelector)).Any(element => element.Displayed);
			int observed = await EscapeClosure.CountUntilClosedAsync(async () =>
			{
				attempt++;
				await TestHelper.Evidence.CaptureAsync(() => TestHelper.NvdaDriver.SendKeysAndGetSpokenTextAsync(NvdaTestingDriver.Key.Escape));
				await Task.Delay(300);
				Console.WriteLine($"Escape {attempt}: dialog open = {IsOpen()}");
			}, IsOpen);
			Assert.AreEqual(expected, observed, "The Escape count must match observed DOM closure, not speech alone.");
		}
	}
}
