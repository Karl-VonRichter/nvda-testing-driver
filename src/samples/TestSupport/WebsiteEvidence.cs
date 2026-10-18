using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvdaTestingDriver;
using NvdaTestingDriver.Commands.NvdaCommands;
using NvdaTestingDriver.Selenium;
using OpenQA.Selenium;

namespace SampleTests
{
	internal sealed class WebsiteEvidence
	{
		private readonly WebDriverWrapper _wrapper;
		private readonly NvdaDriver _nvda;
		private readonly Uri _url;
		private readonly string _pageTitle;
		private readonly string _windowAnnouncement;
		private Stopwatch _sinceTitleReport;

		internal WebsiteEvidence(WebDriverWrapper wrapper, NvdaDriver nvda, Uri url, string pageTitle, string windowAnnouncement)
		{
			_wrapper = wrapper;
			_nvda = nvda;
			_url = url;
			_pageTitle = pageTitle;
			_windowAnnouncement = windowAnnouncement;
		}

		internal static bool MatchesAnnouncement(string actual, string expected) =>
			!string.IsNullOrWhiteSpace(actual) && !string.IsNullOrWhiteSpace(expected) &&
			string.Equals(Normalize(actual), Normalize(expected), StringComparison.OrdinalIgnoreCase);

		private static string Normalize(string value) => Regex.Replace(value.Replace('-', ' '), @"\s+", " ").Trim();

		internal static bool MatchesUrl(string actual, Uri expected) =>
			Uri.TryCreate(actual, UriKind.Absolute, out var current) && current == expected &&
			string.Equals(current.Fragment, expected.Fragment, StringComparison.Ordinal);

		internal static void RequireNamedLandmarkAnnouncement(string name, string roleAnnouncement, string announcement)
		{
			if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(roleAnnouncement) || string.IsNullOrWhiteSpace(announcement))
				Assert.Inconclusive("Named landmark evidence requires a name, role announcement, and complete expected speech.");
			string prefix = Normalize(name + " " + roleAnnouncement);
			string expected = Normalize(announcement);
			if (!string.Equals(expected, prefix, StringComparison.OrdinalIgnoreCase) &&
				!expected.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
				Assert.Inconclusive("The expected landmark announcement must begin with its configured name and spoken role.");
		}

		internal static void RequireCleanEvidence(bool contextVerified, bool foregroundLost, string speech, string expectedAnnouncement)
		{
			if (!contextVerified || foregroundLost ||
				(expectedAnnouncement != null && !MatchesAnnouncement(speech, expectedAnnouncement)))
				Assert.Inconclusive("Speech evidence is incomplete or contaminated; review the private capture. It cannot establish a pass.");
		}

		private bool HasExpectedContext() => _wrapper.IsBrowserWindowForeground() &&
			MatchesUrl(_wrapper.WebDriver.Url, _url) &&
			string.Equals(_wrapper.WebDriver.Title, _pageTitle, StringComparison.Ordinal) &&
			(bool)((IJavaScriptExecutor)_wrapper.WebDriver).ExecuteScript("return document.hasFocus();");

		private async Task<string> ReportWindowTitleAsync()
		{
			// Repeating NVDA+T quickly spells the title. Space context probes beyond NVDA's multi-press interval.
			long remaining = 800 - (_sinceTitleReport?.ElapsedMilliseconds ?? 800);
			if (remaining > 0) await Task.Delay((int)remaining);
			string title = await _nvda.SendCommandAndGetSpokenTextAsync(NavigatingSystemFocusCommands.ReportTitle);
			_sinceTitleReport = Stopwatch.StartNew();
			return title;
		}

		internal async Task<string> CaptureAsync(Func<Task<string>> capture, string expectedAnnouncement = null)
		{
			// Do not reactivate a stolen window here: that would conceal contamination during a capture.
			RequireCleanEvidence(HasExpectedContext(), false, null, null);
			int foregroundLost = 0;
			using var stop = new CancellationTokenSource();
			Task monitor = Task.Run(async () =>
			{
				while (!stop.IsCancellationRequested)
				{
					if (!_wrapper.IsBrowserWindowForeground()) Interlocked.Exchange(ref foregroundLost, 1);
					try { await Task.Delay(50, stop.Token); }
					catch (OperationCanceledException) { break; }
				}
			});
			Action previousGuard = _nvda.BeforeSendingKeys;
			_nvda.BeforeSendingKeys = () => RequireCleanEvidence(HasExpectedContext(), foregroundLost != 0, null, null);
			try
			{
				string before = await ReportWindowTitleAsync();
				Console.WriteLine("Private window announcement before capture: " + before);
				RequireCleanEvidence(HasExpectedContext() && MatchesAnnouncement(before, _windowAnnouncement), foregroundLost != 0, null, null);
				string speech = await capture();
				// Raw speech goes only to private test output, never to the shareable summary.
				Console.WriteLine("Private website capture: " + speech);
				string after = await ReportWindowTitleAsync();
				Console.WriteLine("Private window announcement after capture: " + after);
				RequireCleanEvidence(HasExpectedContext() && MatchesAnnouncement(after, _windowAnnouncement), foregroundLost != 0, speech, expectedAnnouncement);
				return speech;
			}
			finally
			{
				_nvda.BeforeSendingKeys = previousGuard;
				stop.Cancel();
				await monitor;
			}
		}
	}
}
