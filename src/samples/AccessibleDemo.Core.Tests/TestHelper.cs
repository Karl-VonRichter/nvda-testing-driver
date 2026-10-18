using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvdaTestingDriver;
using NvdaTestingDriver.Selenium;
using NvdaTestingDriver.Selenium.Extensions;
using OpenQA.Selenium;
using SampleTests;

namespace AccessibleDemo.Core.Tests
{
	[TestClass]
	public static class TestHelper
	{
		internal static SampleTestSession Session { get; } = new SampleTestSession();
		internal static WebDriverWrapper WebDriverWrapper => Session.Wrapper;
		internal static IWebDriver WebDriver => Session.Browser;
		internal static NvdaDriver NvdaDriver => Session.Nvda;
		internal static string BaseUrl { get; private set; }
		private static Process _webProcess;

		[AssemblyInitialize]
		public static async Task Initialize(TestContext context)
		{
			SampleTestSession.RequireInteractiveDesktop();
			try
			{
				await StartWebAppAsync();
				await Session.StartAsync(context);
			}
			catch
			{
				await Cleanup();
				throw;
			}
		}

		private static async Task StartWebAppAsync()
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			int port = ((IPEndPoint)listener.LocalEndpoint).Port;
			listener.Stop();
			BaseUrl = $"http://127.0.0.1:{port}";
			string project = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AccessibleDemo"));
			string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent.Name;
			string assembly = Path.Combine(project, "bin", configuration, "net10.0", "AccessibleDemo.dll");
			if (!File.Exists(assembly)) throw new FileNotFoundException("Build the local demo before running its tests.", assembly);
			var attributes = Assembly.LoadFrom(assembly).GetCustomAttributes<AssemblyMetadataAttribute>();
			Session.Metadata.Url = BaseUrl;
			Session.Metadata.Organisation = "Local example";
			Session.Metadata.Revision = attributes.FirstOrDefault(a => a.Key == "DeploymentRevision")?.Value ?? "unavailable";
			Session.Metadata.BuildTimestamp = attributes.FirstOrDefault(a => a.Key == "BuildTimestampUtc")?.Value ?? "unavailable";
			Session.Metadata.Save();
			Console.WriteLine("Private run metadata ID: " + Session.Metadata.RunId);
			var start = new ProcessStartInfo("dotnet")
			{
				WorkingDirectory = project,
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			start.ArgumentList.Add(assembly);
			start.ArgumentList.Add("--urls");
			start.ArgumentList.Add(BaseUrl);
			start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
			_webProcess = new Process { StartInfo = start };
			_webProcess.OutputDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
			_webProcess.ErrorDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
			_webProcess.Start();
			_webProcess.BeginOutputReadLine();
			_webProcess.BeginErrorReadLine();
			using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(1) };
			var timer = Stopwatch.StartNew();
			while (timer.Elapsed < TimeSpan.FromSeconds(30))
			{
				if (_webProcess.HasExited) throw new InvalidOperationException("The local demo exited before it was ready.");
				try
				{
					using var response = await client.GetAsync(BaseUrl);
					if (response.IsSuccessStatusCode) return;
				}
				catch (HttpRequestException) { }
				catch (TaskCanceledException) { }
				await Task.Delay(200);
			}
			throw new System.TimeoutException("The local demo did not start within 30 seconds.");
		}

		internal static async Task OpenAsync(string path)
		{
			WebDriver.Navigate().GoToUrl(BaseUrl + path);
			await Session.FocusPageContentAsync();
			WebDriver.Focus(WebDriver.FindElement(By.CssSelector("main")));
			await Task.Delay(500);
		}

		[AssemblyCleanup]
		public static async Task Cleanup()
		{
			try { await Session.StopAsync(); }
			finally
			{
				if (_webProcess != null)
				{
					try
					{
						if (!_webProcess.HasExited) _webProcess.Kill(entireProcessTree: true);
						await _webProcess.WaitForExitAsync();
					}
					finally { _webProcess.Dispose(); _webProcess = null; }
				}
			}
		}
	}
}
