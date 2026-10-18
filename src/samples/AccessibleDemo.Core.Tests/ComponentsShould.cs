using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvdaTestingDriver;
using NvdaTestingDriver.Commands.NvdaCommands;
using NvdaTestingDriver.MSTest;
using NvdaTestingDriver.Selenium.Extensions;
using OpenQA.Selenium;
using SampleTests;

namespace AccessibleDemo.Core.Tests
{
	[TestClass]
	[DoNotParallelize]
	public class ComponentsShould
	{
		public TestContext TestContext { get; set; }

		[TestCleanup]
		public void AttachPrivateMetadata() => TestContext.AddResultFile(TestHelper.Session.Metadata.FilePath);

		[TestInitialize]
		public Task OpenPage() => TestHelper.OpenAsync("/home/components");

		[TestMethod]
		public async Task AnnounceARequiredFieldAndItsDescription()
		{
			TestHelper.WebDriver.Focus(TestHelper.WebDriver.FindElement(By.Id("example-email")));
			await Task.Delay(500);
			string text = await TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(NavigatingSystemFocusCommands.ReportCurrentFocus);
			NvdaAssert.TextContains(text, "Email");
			NvdaAssert.TextContains(text, "required");
			NvdaAssert.TextContains(text, "edit");
			NvdaAssert.TextContains(text, "Enter an email address for this local example.");
		}

		[TestMethod]
		public async Task AnnounceAnEmptyFormError()
		{
			var submit = TestHelper.WebDriver.FindElement(By.CssSelector("#example-form button[type=submit]"));
			TestHelper.WebDriver.Focus(submit);
			await Task.Delay(500);
			Assert.IsTrue((bool)((IJavaScriptExecutor)TestHelper.WebDriver).ExecuteScript("return document.hasFocus() && document.activeElement === arguments[0];", submit));
			string text = await TestHelper.NvdaDriver.GetNextSpokenMessageAsync(System.TimeSpan.FromSeconds(5), System.TimeSpan.FromSeconds(1), () => TestHelper.NvdaDriver.SendKeysAsync(Key.Enter));
			NvdaAssert.TextContains(text, "Enter a valid email address.");
		}

		[TestMethod]
		public async Task DismissANativeDialogWithOneEscape()
		{
			TestHelper.WebDriver.Focus(TestHelper.WebDriver.FindElement(By.Id("open-dialog")));
			await Task.Delay(500);
			string text = await TestHelper.NvdaDriver.SendKeysAndGetSpokenTextAsync(Key.Enter);
			NvdaAssert.TextContains(text, "Example confirmation");
			NvdaAssert.TextContains(text, "dialog");
			int escapes = await EscapeClosure.CountUntilClosedAsync(async () =>
			{
				string speech = await TestHelper.NvdaDriver.SendKeysAndGetSpokenTextAsync(Key.Escape);
				System.Console.WriteLine("Private Escape capture: " + speech);
				await Task.Delay(300);
			}, () => (bool)((IJavaScriptExecutor)TestHelper.WebDriver).ExecuteScript("return document.getElementById('example-dialog').open;"));
			Assert.AreEqual(1, escapes, "A one-Escape claim requires observed closure after the first Escape.");
			Assert.AreEqual("open-dialog", TestHelper.WebDriver.SwitchTo().ActiveElement().GetAttribute("id"));
		}

		[TestMethod]
		public async Task NavigateTableColumnsAndRows()
		{
			await TestHelper.NvdaDriver.SendKeysAsync(Key.Control, Key.Home);
			string text = await TestHelper.NvdaDriver.SendKeysAndGetSpokenTextAsync(Key.T);
			NvdaAssert.TextContains(text, "Example inventory");
			NvdaAssert.TextContains(text, "table");
			// The table quick-navigation key lands on the caption; move into its first header cell.
			await TestHelper.NvdaDriver.SendKeysAsync(Key.DownArrow);
			text = await TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(NavigatingSystemCaretCommands.MoveToNextColumn);
			NvdaAssert.TextContains(text, "Quantity");
			text = await TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(NavigatingSystemCaretCommands.MoveToNextRow);
			NvdaAssert.TextContains(text, "12");
			text = await TestHelper.NvdaDriver.SendCommandAndGetSpokenTextAsync(NavigatingSystemCaretCommands.MoveToPreviousColumn);
			NvdaAssert.TextContains(text, "Pencils");
		}
	}
}
