using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvdaTestingDriver.Commands.NvdaCommands;
using NvdaTestingDriver.MSTest;
using NvdaTestingDriver.Selenium.Extensions;
using System.Threading.Tasks;

namespace AccessibleDemo.Core.Tests
{
	[TestClass]
	[DoNotParallelize]
	public class ContactShould
	{
		public TestContext TestContext { get; set; }

		[TestCleanup]
		public void AttachPrivateMetadata() => TestContext.AddResultFile(TestHelper.Session.Metadata.FilePath);


		[TestMethod]
		public async Task CheckContactHeadersReadCollapsiblePannels()
		{
			// Arrange:
			await TestHelper.OpenAsync("/home/contact");

			// Act & asserts
			string text = await TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(BrowseModeCommands.NextHeading3);
			NvdaAssert.TextContains(text, "Contact by phone");
			NvdaAssert.TextContains(text, "collapsed");

			text = await TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(BrowseModeCommands.NextHeading3);
			NvdaAssert.TextContains(text, "Contact by e-mail");

			text = await TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(BrowseModeCommands.NextHeading3);
			NvdaAssert.TextContains(text, "Postal address");

			text = await TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(BrowseModeCommands.NextHeading3);
			NvdaAssert.TextContains(text, "Fill the contact form");
		}

		[TestMethod]
		public async Task CheckContactFormLabelsReadFieldAsEspected()
		{
			// Arrange
			await TestHelper.OpenAsync("/home/contact");
			TestHelper.WebDriver.Focus(TestHelper.WebDriver.FindElement(OpenQA.Selenium.By.CssSelector("a[data-bs-target='#contactForm']")));
			await TestHelper.NvdaDriver.SendKeysAsync(NvdaTestingDriver.Key.Enter);
			await Task.Delay(500);

			// Verify the accessible labels directly after the collapsed form becomes visible.
			TestHelper.WebDriver.Focus(TestHelper.WebDriver.FindElement(OpenQA.Selenium.By.Id("name")));
			await Task.Delay(600);
			string text = await TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(NavigatingSystemFocusCommands.ReportCurrentFocus);
			NvdaAssert.TextContains(text, "Name:");
			NvdaAssert.TextContains(text, "edit");

			TestHelper.WebDriver.Focus(TestHelper.WebDriver.FindElement(OpenQA.Selenium.By.Id("email")));
			await Task.Delay(600);
			text = await TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(NavigatingSystemFocusCommands.ReportCurrentFocus);
			NvdaAssert.TextContains(text, "E mail:");
			NvdaAssert.TextContains(text, "edit");

			TestHelper.WebDriver.Focus(TestHelper.WebDriver.FindElement(OpenQA.Selenium.By.CssSelector("#contactForm button")));
			await Task.Delay(600);
			text = await TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(NavigatingSystemFocusCommands.ReportCurrentFocus);
			NvdaAssert.TextContains(text, "Submit");
			NvdaAssert.TextContains(text, "button");
		}
	}
	}
