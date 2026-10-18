using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using SampleTests;

namespace NvdaTestingDriver.Tests
{
	[TestClass]
	[DoNotParallelize]
	public class RunMetadataShould
	{
		[TestMethod]
		public void ExpandTheVisibleVersionWhenAHiddenCopyPrecedesIt()
		{
			string previous = Environment.GetEnvironmentVariable("NVDA_TEST_PRIVATE_ARTIFACTS");
			string directory = Path.Combine(Path.GetTempPath(), "nvda-metadata-" + Guid.NewGuid().ToString("N"));
			var metadata = new RunMetadata();
			try
			{
				Environment.SetEnvironmentVariable("NVDA_TEST_PRIVATE_ARTIFACTS", directory);
				var browser = DispatchProxy.Create<IWebDriver, BrowserProxy>();
				metadata.ReadDeployment(browser, "toggle", "revision", "timestamp", "organisation");
				Assert.IsTrue(((BrowserProxy)browser).Clicked);
				Assert.AreEqual("abcdef0123456789abcdef0123456789abcdef01", metadata.Revision);
				Assert.AreEqual("2026-10-07T03:04:05.000Z", metadata.BuildTimestamp);
				Assert.IsTrue(metadata.HasDeploymentMetadata);
			}
			finally
			{
				Environment.SetEnvironmentVariable("NVDA_TEST_PRIVATE_ARTIFACTS", previous);
				if (metadata.FilePath != null) File.Delete(metadata.FilePath);
				if (Directory.Exists(directory)) Directory.Delete(directory);
			}
		}

		public class BrowserProxy : DispatchProxy, IJavaScriptExecutor
		{
			public bool Clicked { get; private set; }
			protected override object Invoke(MethodInfo method, object[] args)
			{
				if (method.Name == "get_Url") return "https://example.invalid/";
				if (method.Name == "FindElements")
				{
					string selector = args[0].ToString();
					if (selector.EndsWith(": toggle", StringComparison.Ordinal))
						return new ReadOnlyCollection<IWebElement>(new[] {
							Element(false, "", () => throw new InvalidOperationException("Hidden toggle must not be clicked.")),
							Element(true, "", () => Clicked = true) });
					string text = selector.EndsWith(": revision", StringComparison.Ordinal) ? "abcdef0123456789abcdef0123456789abcdef01" :
						selector.EndsWith(": timestamp", StringComparison.Ordinal) ? "7 October 2026" : "Example organisation";
					return new ReadOnlyCollection<IWebElement>(new[] { Element(true, text, () => { }) });
				}
				throw new NotSupportedException(method.Name);
			}
			public object ExecuteScript(string script, params object[] args) => false;
			public object ExecuteAsyncScript(string script, params object[] args) => throw new NotSupportedException();
			public object ExecuteScript(PinnedScript script, params object[] args) => false;
			private static IWebElement Element(bool displayed, string text, Action click)
			{
				var element = DispatchProxy.Create<IWebElement, ElementProxy>();
				var proxy = (ElementProxy)element;
				proxy.Displayed = displayed;
				proxy.Text = text;
				proxy.Click = click;
				return element;
			}
		}

		public class ElementProxy : DispatchProxy
		{
			public bool Displayed { get; set; }
			public string Text { get; set; }
			public Action Click { get; set; }
			protected override object Invoke(MethodInfo method, object[] args)
			{
				if (method.Name == "get_Displayed") return Displayed;
				if (method.Name == "get_Text") return Text;
				if (method.Name == "get_TagName") return Text == "7 October 2026" ? "time" : "div";
				if (method.Name == "GetDomAttribute") return "2026-10-07T03:04:05.000Z";
				if (method.Name == "GetAttribute") return "false";
				if (method.Name == "Click") { Click(); return null; }
				throw new NotSupportedException(method.Name);
			}
		}
	}
}
