using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvdaTestingDriver;
using NvdaTestingDriver.MSTest;
using NvdaTestingDriver.Selenium.Extensions;
using OpenQA.Selenium;
using System.Threading.Tasks;

namespace AccessibleDemo.Core.Tests
{

	[TestClass]
	[DoNotParallelize]
	public class TreeViewAccessibleShould
	{
		public TestContext TestContext { get; set; }

		[TestCleanup]
		public void AttachPrivateMetadata() => TestContext.AddResultFile(TestHelper.Session.Metadata.FilePath);


		[TestMethod]
		public async Task CheckTreeViewInteraction()
		{
			// arrange
			await TestHelper.OpenAsync("/home/TreeViewExample");
			for (int attempt = 0; attempt < 20 && TestHelper.WebDriver.FindElements(By.CssSelector(".treeview .toggle")).Count == 0; attempt++)
			{
				await Task.Delay(250);
			}
			Assert.IsTrue(TestHelper.WebDriver.FindElements(By.CssSelector(".treeview .toggle")).Count > 0, "The tree keyboard handlers did not initialize.");
			TestHelper.WebDriver.Focus(TestHelper.WebDriver.FindElement(By.Id("lnkWcag")));

			// act & asert
			string text = await TestHelper.NvdaDriver.SendKeysAndGetSpokenTextAsync(Key.Tab);
			NvdaAssert.TextContains(text, "All WCAG 2.1 elements");
			NvdaAssert.TextContains(text, "tree view");
			NvdaAssert.TextContains(text, "1. Perceivable");
			NvdaAssert.TextContains(text, "collapsed");
			await Task.Delay(700);
			Assert.AreEqual("treeitem", TestHelper.WebDriver.SwitchTo().ActiveElement().GetAttribute("role"));

			text = await TestHelper.NvdaDriver.GetNextSpokenMessageAsync(System.TimeSpan.FromSeconds(5), null, async () =>
			{
				await TestHelper.NvdaDriver.SendKeysAsync(Key.RightArrow);
				await Task.Delay(300);
				System.Console.WriteLine("Tree after Right: " + ((IJavaScriptExecutor)TestHelper.WebDriver).ExecuteScript("return JSON.stringify({focus:document.activeElement.textContent,expanded:document.activeElement.getAttribute('aria-expanded'),pageFocus:document.hasFocus()});"));
			});
			NvdaAssert.TextContains(text, "Expanded");

			text = await TestHelper.NvdaDriver.SendKeysAndGetSpokenTextAsync(Key.DownArrow);
			NvdaAssert.TextContains(text, "1.1. Text alternatives");

			text = await TestHelper.NvdaDriver.SendKeysAndGetSpokenTextAsync(Key.DownArrow);
			NvdaAssert.TextContains(text, "1.2. Time-based Media");

			text = await TestHelper.NvdaDriver.SendKeysAndGetSpokenTextAsync(Key.DownArrow);
			NvdaAssert.TextContains(text, "1.3. Adaptable");

			text = await TestHelper.NvdaDriver.SendKeysAndGetSpokenTextAsync(Key.DownArrow);
			NvdaAssert.TextContains(text, "1.4. Distinguishable");

			text = await TestHelper.NvdaDriver.SendKeysAndGetSpokenTextAsync(Key.DownArrow);
			NvdaAssert.TextContains(text, "2. Operable");

			text = await TestHelper.NvdaDriver.SendKeysAndGetSpokenTextAsync(Key.RightArrow);
			NvdaAssert.TextContains(text, "Expanded");

			text = await TestHelper.NvdaDriver.SendKeysAndGetSpokenTextAsync(Key.LeftArrow);
			NvdaAssert.TextContains(text, "collapsed");

		}

	}
}
