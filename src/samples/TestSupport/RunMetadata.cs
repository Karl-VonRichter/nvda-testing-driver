using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using OpenQA.Selenium;

namespace SampleTests
{
	internal sealed class RunMetadata
	{
		public string RunId { get; } = Guid.NewGuid().ToString("N");
		public string StartedUtc { get; } = DateTimeOffset.UtcNow.ToString("O");
		public string Browser { get; set; } = "unavailable";
		public string BrowserVersion { get; set; } = "unavailable";
		public string NvdaVersion { get; set; } = "unavailable";
		public string Url { get; set; } = "unavailable";
		public string Organisation { get; set; } = "unavailable";
		public string Revision { get; set; } = "unavailable";
		public string BuildTimestamp { get; set; } = "unavailable";
		public string Status { get; set; } = "starting";
		internal string FilePath { get; private set; }
		internal bool HasDeploymentMetadata => Revision != "unavailable" && BuildTimestamp != "unavailable" && Organisation != "unavailable";

		internal void Save()
		{
			if (FilePath == null)
			{
				string directory = Environment.GetEnvironmentVariable("NVDA_TEST_PRIVATE_ARTIFACTS") ?? Path.Combine("artifacts", "private");
				Directory.CreateDirectory(directory);
				FilePath = Path.GetFullPath(Path.Combine(directory, "run-" + RunId + ".json"));
			}
			File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
		}

		internal void ReadDeployment(IWebDriver browser, string toggleSelector, string revisionSelector, string timestampSelector, string organisationSelector)
		{
			Url = browser.Url;
			try
			{
				if (!string.IsNullOrWhiteSpace(toggleSelector))
				{
					var toggle = browser.FindElements(By.CssSelector(toggleSelector)).FirstOrDefault(element => element.Displayed)
						?? throw new InvalidOperationException("The configured Version toggle was not visible.");
					bool detailsOpen = (bool)((IJavaScriptExecutor)browser).ExecuteScript("return arguments[0].tagName === 'SUMMARY' && arguments[0].parentElement.open === true;", toggle);
					if (toggle.GetAttribute("aria-expanded") != "true" && !detailsOpen) toggle.Click();
				}
				Revision = ReadVisibleText(browser, revisionSelector);
				BuildTimestamp = ReadVisibleText(browser, timestampSelector, readTimeAttribute: true);
				Organisation = ReadVisibleText(browser, organisationSelector);
				Status = HasDeploymentMetadata ? "deployment metadata captured" : "deployment metadata incomplete";
			}
			catch { Status = "deployment metadata capture failed"; throw; }
			finally { Save(); }
		}

		private static string ReadVisibleText(IWebDriver browser, string selector, bool readTimeAttribute = false)
		{
			if (string.IsNullOrWhiteSpace(selector)) return "unavailable";
			// Expanded sections may render asynchronously. No deployment values are inferred from the harness checkout.
			var until = DateTime.UtcNow.AddSeconds(5);
			do
			{
				foreach (var element in browser.FindElements(By.CssSelector(selector)))
					if (element.Displayed && !string.IsNullOrWhiteSpace(element.Text))
					{
						if (readTimeAttribute && string.Equals(element.TagName, "time", StringComparison.OrdinalIgnoreCase))
						{
							string timestamp = element.GetDomAttribute("datetime");
							if (!string.IsNullOrWhiteSpace(timestamp)) return timestamp.Trim();
						}
						return element.Text.Trim();
					}
				System.Threading.Thread.Sleep(100);
			} while (DateTime.UtcNow < until);
			throw new InvalidOperationException("Configured deployment metadata was not visible after expanding Version.");
		}
	}
}
