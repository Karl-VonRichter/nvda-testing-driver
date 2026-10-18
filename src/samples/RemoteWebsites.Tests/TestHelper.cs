using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvdaTestingDriver;
using OpenQA.Selenium;
using SampleTests;

namespace RemoteWebsites.Tests
{
	[TestClass]
	public static class TestHelper
	{
		internal static SampleTestSession Session { get; } = new SampleTestSession();
		internal static IWebDriver WebDriver => Session.Browser;
		internal static NvdaDriver NvdaDriver => Session.Nvda;
		internal static Uri PageUrl { get; private set; }
		internal static string ElementSelector { get; private set; }
		internal static string ExpectedText { get; private set; }
		internal static WebsiteEvidence Evidence { get; private set; }

		[AssemblyInitialize]
		public static async Task Initialize(TestContext context)
		{
			SampleTestSession.RequireInteractiveDesktop();
			string url = Environment.GetEnvironmentVariable("NVDA_TEST_PAGE_URL");
			ElementSelector = Environment.GetEnvironmentVariable("NVDA_TEST_ELEMENT_SELECTOR");
			ExpectedText = Environment.GetEnvironmentVariable("NVDA_TEST_EXPECTED_TEXT");
			if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(ElementSelector) || string.IsNullOrWhiteSpace(ExpectedText))
				Assert.Inconclusive("Configure NVDA_TEST_PAGE_URL, NVDA_TEST_ELEMENT_SELECTOR, and NVDA_TEST_EXPECTED_TEXT to run this example.");
			if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
				(uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) || !string.IsNullOrEmpty(uri.UserInfo))
				throw new ArgumentException("NVDA_TEST_PAGE_URL must be an HTTP(S) URL without embedded credentials.");
			PageUrl = uri;
			string title = Environment.GetEnvironmentVariable("NVDA_TEST_PAGE_TITLE");
			string announcement = Environment.GetEnvironmentVariable("NVDA_TEST_WINDOW_ANNOUNCEMENT");
			if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(announcement))
				Assert.Inconclusive("Configure the expected page title and exact NVDA window announcement to establish website context.");
			Session.Metadata.Url = uri.AbsoluteUri;
			try
			{
				await Session.StartAsync(context);
				WebDriver.Navigate().GoToUrl(PageUrl.AbsoluteUri);
				Session.Metadata.ReadDeployment(WebDriver,
					Environment.GetEnvironmentVariable("NVDA_TEST_VERSION_TOGGLE_SELECTOR"),
					Environment.GetEnvironmentVariable("NVDA_TEST_REVISION_SELECTOR"),
					Environment.GetEnvironmentVariable("NVDA_TEST_BUILD_TIMESTAMP_SELECTOR"),
					Environment.GetEnvironmentVariable("NVDA_TEST_ORGANISATION_SELECTOR"));
				if (!Session.Metadata.HasDeploymentMetadata)
					Assert.Inconclusive("Configure revision, build timestamp, and organisation selectors; deployment identity is required for a conclusive run.");
				Evidence = new WebsiteEvidence(Session.Wrapper, Session.Nvda, PageUrl, title, announcement);
			}
			catch
			{
				await Session.StopAsync();
				throw;
			}
		}

		[AssemblyCleanup]
		public static Task Cleanup() => Session.StopAsync();
	}
}
