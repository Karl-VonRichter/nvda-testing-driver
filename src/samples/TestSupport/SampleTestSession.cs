using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvdaTestingDriver;
using NvdaTestingDriver.Selenium;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Chromium;
using OpenQA.Selenium.Edge;

namespace SampleTests
{
	// Shared by the local and configurable examples. Private run metadata is never exported with source.
	internal sealed class SampleTestSession
	{
		internal WebDriverWrapper Wrapper { get; } = new WebDriverWrapper();
		internal IWebDriver Browser => Wrapper.WebDriver;
		internal NvdaDriver Nvda { get; private set; }
		internal RunMetadata Metadata { get; } = new RunMetadata();
		private bool _stopped = true;

		internal static void RequireInteractiveDesktop()
		{
			if (!string.Equals(Environment.GetEnvironmentVariable("NVDA_TEST_RUN_INTERACTIVE"), "true", StringComparison.OrdinalIgnoreCase))
				Assert.Inconclusive("Set NVDA_TEST_RUN_INTERACTIVE=true on an interactive Windows desktop to run screen-reader samples.");
		}

		internal async Task StartAsync(TestContext context = null)
		{
			Metadata.Save();
			Console.WriteLine("Private run metadata ID: " + Metadata.RunId);
			context?.AddResultFile(Metadata.FilePath);
			string browser = (Environment.GetEnvironmentVariable("NVDA_TEST_BROWSER") ?? "chrome").ToLowerInvariant();
			if (browser != "chrome" && browser != "chromium" && browser != "edge")
				throw new ArgumentException("NVDA_TEST_BROWSER must be chrome, chromium, or edge.");
			try
			{
				_stopped = false;
				Wrapper.UpWebDriver(() =>
				{
					ChromiumOptions options = browser == "edge" ? new EdgeOptions() : new ChromeOptions();
					options.AddArgument("--force-renderer-accessibility");
					options.AddExcludedArgument("enable-automation");
					options.AddUserProfilePreference("credentials_enable_service", false);
					options.AddUserProfilePreference("profile.password_manager_enabled", false);
					string binary = Environment.GetEnvironmentVariable("NVDA_TEST_BROWSER_BINARY");
					if (!string.IsNullOrWhiteSpace(binary)) options.BinaryLocation = binary;
					else if (browser == "chromium") options.BrowserVersion = "stable";
					IWebDriver driver = options is EdgeOptions edge ? new EdgeDriver(edge) : new ChromeDriver((ChromeOptions)options);
					try
					{
						var capabilities = ((IHasCapabilities)driver).Capabilities;
						Metadata.Browser = capabilities.GetCapability("browserName")?.ToString() ?? "unavailable";
						Metadata.BrowserVersion = capabilities.GetCapability("browserVersion")?.ToString() ?? "unavailable";
						Metadata.Save();
						return driver;
					}
					catch
					{
						try { driver.Quit(); }
						finally { driver.Dispose(); }
						throw;
					}
				});
				Nvda = new NvdaDriver(options =>
				{
					options.NvdaExecutablePath = Environment.GetEnvironmentVariable("NVDA_TEST_NVDA_EXECUTABLE");
					options.UseInstalledNvda = !string.Equals(Environment.GetEnvironmentVariable("NVDA_TEST_USE_INSTALLED"), "false", StringComparison.OrdinalIgnoreCase);
					options.BrowseModeSettings.AutoSayAllOnPageLoad = false;
				});
				Nvda.BeforeSendingKeys = Wrapper.EnsureBrowserWindowForeground;
				await Nvda.ConnectAsync();
				Metadata.NvdaVersion = Nvda.NvdaVersion ?? "unavailable";
				Metadata.Status = "connected";
				Metadata.Save();
			}
			catch
			{
				try
				{
					Metadata.NvdaVersion = Nvda?.NvdaVersion ?? "unavailable";
					Metadata.Status = "startup failed";
					Metadata.Save();
				}
				finally { await StopAsync(); }
				throw;
			}
		}

		internal async Task FocusPageContentAsync()
		{
			Wrapper.EnsureBrowserWindowForeground();
			for (int attempt = 0; attempt < 6 && !PageHasFocus(); attempt++)
			{
				await Nvda.SendKeysAsync(Key.F6);
				await Task.Delay(400);
			}
			Assert.IsTrue(PageHasFocus(), "Could not move keyboard focus into the test page.");
		}

		private bool PageHasFocus() => (bool)((IJavaScriptExecutor)Browser).ExecuteScript("return document.hasFocus();");

		internal async Task StopAsync()
		{
			if (_stopped) return;
			_stopped = true;
			try
			{
				if (Nvda != null) await Nvda.DisconnectAsync();
			}
			finally
			{
				try { Browser?.Quit(); }
				finally { Browser?.Dispose(); }
			}
		}
	}
}
