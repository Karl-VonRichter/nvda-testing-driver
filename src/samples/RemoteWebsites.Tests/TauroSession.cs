using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using OpenQA.Selenium;

namespace RemoteWebsites.Tests
{
	/// <summary>
	/// Signs in to the Tauro dashboard, and switches the browser between signed-in and signed-out.
	/// The dashboard keeps its session in a session-only cookie, so the browser has to sign in on every run.
	/// </summary>
	internal static class TauroSession
	{
		internal const string BaseUrl = "https://dashboard.tauro-research.com";

		private static readonly TimeSpan ManualSignInTimeout = TimeSpan.FromMinutes(10);

		private static List<Cookie> _sessionCookies = new List<Cookie>();

		/// <summary>
		/// Gets a value indicating whether the browser was signed in when the run started.
		/// </summary>
		internal static bool IsSignedIn => _sessionCookies.Count > 0;

		/// <summary>
		/// Signs in with TAURO_EMAIL / TAURO_PASSWORD (environment variables, or lines in %USERPROFILE%\.tauro-nvda-test).
		/// Without them, waits for someone to sign in by hand in the test browser.
		/// </summary>
		internal static void SignIn()
		{
			var driver = TestHelper.WebDriver;
			driver.Navigate().GoToUrl(BaseUrl + "/login");
			WaitFor(() => driver.FindElements(By.CssSelector("input[name=email]")).Count > 0 || !OnSignInPage(), TimeSpan.FromSeconds(20));

			var (email, password) = ReadCredentials();
			if (OnSignInPage() && email != null && password != null)
			{
				driver.FindElement(By.CssSelector("input[name=email]")).SendKeys(email);
				driver.FindElement(By.CssSelector("input[name=password]")).SendKeys(password);
				driver.FindElement(By.CssSelector("button[type=submit]")).Click();
				WaitFor(() => !OnSignInPage(), TimeSpan.FromSeconds(30));
			}
			else if (OnSignInPage())
			{
				Console.WriteLine($"Sign in to the Tauro dashboard in the test browser window (waiting up to {ManualSignInTimeout.TotalMinutes} minutes).");
				WaitFor(() => !OnSignInPage(), ManualSignInTimeout);
			}

			if (OnSignInPage())
			{
				Console.WriteLine("Not signed in: the dashboard tests will be inconclusive.");
				return;
			}

			_sessionCookies = driver.Manage().Cookies.AllCookies.ToList();
		}

		/// <summary>
		/// Makes sure the browser is signed in, restoring the session cookies if a signed-out test removed them.
		/// </summary>
		internal static void EnsureSignedIn()
		{
			if (!IsSignedIn)
			{
				Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Inconclusive("Not signed in to the Tauro dashboard.");
			}

			var cookies = TestHelper.WebDriver.Manage().Cookies;
			if (_sessionCookies.All(c => cookies.GetCookieNamed(c.Name) != null))
			{
				return;
			}

			// Cookies can only be set for the site that is loaded.
			TestHelper.WebDriver.Navigate().GoToUrl(BaseUrl + "/login");
			foreach (var cookie in _sessionCookies)
			{
				cookies.AddCookie(cookie);
			}
		}

		/// <summary>
		/// Signs the browser out, without ending the session on the server, so it can be restored afterwards.
		/// </summary>
		internal static void EnsureSignedOut()
		{
			TestHelper.WebDriver.Navigate().GoToUrl(BaseUrl + "/login");
			TestHelper.WebDriver.Manage().Cookies.DeleteAllCookies();
		}

		private static bool OnSignInPage()
		{
			var path = new Uri(TestHelper.WebDriver.Url).AbsolutePath;
			return path.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
				|| TestHelper.WebDriver.FindElements(By.CssSelector("input[name=password]")).Count > 0;
		}

		private static (string Email, string Password) ReadCredentials()
		{
			string email = Environment.GetEnvironmentVariable("TAURO_EMAIL");
			string password = Environment.GetEnvironmentVariable("TAURO_PASSWORD");
			string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".tauro-nvda-test");
			if ((email == null || password == null) && File.Exists(file))
			{
				var values = File.ReadAllLines(file)
					.Select(l => l.Split(new[] { '=' }, 2))
					.Where(p => p.Length == 2)
					.ToDictionary(p => p[0].Trim(), p => p[1].Trim());
				email = email ?? (values.TryGetValue("TAURO_EMAIL", out var e) ? e : null);
				password = password ?? (values.TryGetValue("TAURO_PASSWORD", out var p) ? p : null);
			}

			return (email, password);
		}

		private static void WaitFor(Func<bool> condition, TimeSpan timeout)
		{
			var deadline = DateTime.Now + timeout;
			while (DateTime.Now < deadline)
			{
				try
				{
					if (condition())
					{
						return;
					}
				}
				catch (WebDriverException)
				{
					// The page may be navigating.
				}

				Thread.Sleep(500);
			}
		}
	}
}
