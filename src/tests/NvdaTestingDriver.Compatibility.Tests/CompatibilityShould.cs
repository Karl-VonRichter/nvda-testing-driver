using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvdaTestingDriver.Commands.NvdaCommands;
using NvdaTestingDriver.MSTest;
using NvdaTestingDriver.Selenium;
using NvdaTestingDriver.Settings;

namespace NvdaTestingDriver.Compatibility.Tests
{
	[TestClass]
	public class CompatibilityShould
	{
		[TestMethod]
		public void LoadAllLibrariesOnNetFramework()
		{
			NvdaAssert.TextContains("Example heading  level 1", "heading level 1");
			Assert.AreEqual(1, NavigatingSystemFocusCommands.ReportCurrentFocus.DesktopCombinationSet.Count);
			Assert.IsNotNull(new WebDriverWrapper());
		}
	}
}
