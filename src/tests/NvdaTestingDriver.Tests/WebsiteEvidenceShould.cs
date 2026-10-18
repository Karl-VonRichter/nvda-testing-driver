using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SampleTests;

namespace NvdaTestingDriver.Tests
{
	[TestClass]
	public class WebsiteEvidenceShould
	{
		[TestMethod]
		public async Task CountTwoEscapesWhenTheFirstOnlyChangesMode()
		{
			int sent = 0;
			int count = await EscapeClosure.CountUntilClosedAsync(() => { sent++; return Task.CompletedTask; }, () => sent < 2);
			Assert.AreEqual(2, count);
		}

		[TestMethod]
		public async Task RefuseClosureEvidenceForAnAlreadyClosedDialog()
		{
			await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => EscapeClosure.CountUntilClosedAsync(() => Task.CompletedTask, () => false));
		}

		[TestMethod]
		public async Task RefuseClosureEvidenceWhenTheDialogRemainsOpen()
		{
			await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => EscapeClosure.CountUntilClosedAsync(() => Task.CompletedTask, () => true));
		}

		[TestMethod]
		[DataRow(false, false, "Main navigation navigation landmark")]
		[DataRow(true, true, "Main navigation navigation landmark")]
		[DataRow(true, false, "Other application notification Main navigation navigation landmark")]
		[DataRow(true, false, "navigation landmark")]
		[DataRow(true, false, "")]
		public void MakeMissingContextAndContaminatedLandmarksInconclusive(bool context, bool lostForeground, string speech)
		{
			Assert.ThrowsExactly<AssertInconclusiveException>(() => WebsiteEvidence.RequireCleanEvidence(context, lostForeground, speech, "Main navigation navigation landmark"));
		}

		[TestMethod]
		public void AcceptOnlyTheNamedLandmarkWithVerifiedContext()
		{
			WebsiteEvidence.RequireCleanEvidence(true, false, "Main navigation\r\n navigation landmark", "Main navigation navigation landmark");
		}

		[TestMethod]
		public void DistinguishHashRoutesOnTheSameWebsite()
		{
			var expected = new Uri("https://example.org/#/dashboard");
			Assert.IsFalse(WebsiteEvidence.MatchesUrl("https://example.org/#/other", expected));
			Assert.IsTrue(WebsiteEvidence.MatchesUrl("https://example.org/#/dashboard", expected));
		}

		[TestMethod]
		[DataRow("Main navigation", "navigation landmark")]
		[DataRow("Navigation", "navigation landmark")]
		[DataRow("Main", "Remaining navigation landmark")]
		public void RejectLandmarkAnnouncementsWithoutTheirNameAndRole(string name, string speech)
		{
			Assert.ThrowsExactly<AssertInconclusiveException>(() => WebsiteEvidence.RequireNamedLandmarkAnnouncement(name, "navigation landmark", speech));
		}

		[TestMethod]
		public void AcceptTheNamedLandmarkWithItsRoleAndDescendantSpeech()
		{
			WebsiteEvidence.RequireNamedLandmarkAnnouncement("Main navigation", "navigation landmark", "Main navigation navigation landmark list with 4 items Home link");
		}
	}
}
