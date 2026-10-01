using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvdaTestingDriver;
using NvdaTestingDriver.Selenium;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Chromium;
using OpenQA.Selenium.Edge;

namespace RemoteWebsites.Tests
{
	[TestClass]
	public static class TestHelper
	{

		internal static WebDriverWrapper WebDriverWrapper = new WebDriverWrapper();

		internal static IWebDriver WebDriver { get; private set; }


		internal static NvdaDriver NvdaDriver;

		/// <summary>
		/// This method will be executed before starting any test
		/// </summary>
		/// <param name="context">The context.</param>
		/// <returns>The task associated to this operation</returns>
		[AssemblyInitialize]
		public static async Task Initialize(TestContext context)
		{

			// Initialize the Selenium WebDriveer
			UpWebDriver();

			// Sign in to the Tauro dashboard before NVDA starts, so a manual sign-in doesn't fight with NVDA's keystrokes.
			TauroSession.SignIn();

			// Starts the NVDATestingDriver
			await ConnectNvdaDriverAsync();
		}

		private static void UpWebDriver()
		{
			try
			{

				// We started the WebDriver using the UpWebDriver method of the WebDriverWrapper class,
				// to manage the chrome window, and get to put it in the foreground when necessary.
				WebDriver = WebDriverWrapper.UpWebDriver(() =>
				{
					// The browser is chosen with the NVDA_TEST_BROWSER environment variable:
					//   chrome (default) - the installed Google Chrome
					//   edge             - the installed Microsoft Edge (Chromium)
					//   chromium         - Chrome for Testing, downloaded by Selenium Manager,
					//                      or the Chromium binary in NVDA_TEST_BROWSER_BINARY if set
					string browser = (Environment.GetEnvironmentVariable("NVDA_TEST_BROWSER") ?? "chrome").ToLowerInvariant();
					string binary = Environment.GetEnvironmentVariable("NVDA_TEST_BROWSER_BINARY");
					ChromiumOptions op = browser == "edge" ? new EdgeOptions() : new ChromeOptions();
					op.AcceptInsecureCertificates = false;

					// Build the accessibility tree up front, even if Chromium starts before NVDA.
					op.AddArgument("--force-renderer-accessibility");

					// Hide the "controlled by automated test software" bar and the save-password bubble,
					// which NVDA would otherwise read out and which can take focus.
					op.AddExcludedArgument("enable-automation");
					op.AddUserProfilePreference("credentials_enable_service", false);
					op.AddUserProfilePreference("profile.password_manager_enabled", false);
					if (browser == "chromium")
					{
						if (string.IsNullOrWhiteSpace(binary))
						{
							op.BrowserVersion = "stable";
						}
						else
						{
							op.BinaryLocation = binary;
						}
					}

					IWebDriver webDriver = op is EdgeOptions edgeOptions ? new EdgeDriver(edgeOptions) : new ChromeDriver((ChromeOptions)op);
					webDriver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromMinutes(3);
					webDriver.Manage().Window.Maximize();
					return webDriver;
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error while starting WebDriver: {ex.Message}");
				throw;
			}
		}


		/// <summary>
		/// Connects the nvda driver asynchronously.
		/// </summary>
		/// <returns></returns>
		private static async Task ConnectNvdaDriverAsync()
		{
			try
			{
				// We start the NvdaTestingDriver:
				NvdaDriver = new NvdaDriver(opt =>
				{
					opt.GeneralSettings.Language = NvdaTestingDriver.Settings.NvdaLanguage.English;

					// Reading the whole page on load would talk over the announcements under test.
					opt.BrowseModeSettings.AutoSayAllOnPageLoad = false;
				});

				// Keys sent through NVDA are real keystrokes: refuse to send them unless the test browser is in the foreground.
				NvdaDriver.BeforeSendingKeys = WebDriverWrapper.EnsureBrowserWindowForeground;
				await NvdaDriver.ConnectAsync();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error while starting NVDA driver: {ex.Message}");
				throw;
			}
		}

		/// <summary>
		/// This method will be executed when all e tests are finished.
		/// </summary>
		/// <returns></returns>
		[AssemblyCleanup]
		public static async Task Cleanup()
		{
			try
			{
				WebDriver.Quit();
			}
			catch
			{
				// If the web  driver quit fails, we continue.
			}

			try
			{
				WebDriver.Dispose();
			}
			catch
			{
				// If the web  driver dispose fails, we continue.
			}

			try
			{
				await NvdaDriver.DisconnectAsync();
			}
			catch
			{
				// if disconnect fails, we continue.
			}
		}



	}
}
