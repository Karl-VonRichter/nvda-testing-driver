using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvdaTestingDriver.MSTest;

namespace NvdaTestingDriver.Tests
{
	[TestClass]
	public class TextShould
	{
		[TestMethod]
		[DataRow(" Email\r\n\t EDIT  required ", "email edit required")]
		[DataRow("E - mail edit", "E-mail edit")]
		[DataRow("E-mail edit", "E mail edit")]
		public void MatchSpokenWhitespaceAndHyphens(string actual, string expected)
		{
			Assert.IsTrue(NvdaTestHelper.TextContains(actual, expected));
			NvdaAssert.TextContains(actual, expected);
		}

		[TestMethod]
		public void FailWhenTheExpectedAccessibleNameIsMissing()
		{
			Assert.IsFalse(NvdaTestHelper.TextContains("Email edit", "required"));
			Assert.ThrowsExactly<AssertFailedException>(() => NvdaAssert.TextContains("Email edit", "required"));
		}

		[TestMethod]
		public void RejectNullTextArguments()
		{
			Assert.ThrowsExactly<ArgumentNullException>(() => NvdaTestHelper.TextContains(null, "Name"));
			Assert.ThrowsExactly<ArgumentNullException>(() => NvdaTestHelper.TextContains("Name", null));
		}
	}
}
