// Copyright (C) 2019 Juan José Montiel
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using OpenQA.Selenium;

namespace NvdaTestingDriver.Selenium
{
	/// <summary>
	/// Class that Wraps Selenium WebDriver, and provides it with the extra functionality needed for NVDA testing.
	/// /// </summary>
	public class WebDriverWrapper
	{
		private IntPtr _browserWindowHandle = IntPtr.Zero;

		/// <summary>
		/// Gets the web driver.
		/// </summary>
		/// <value>
		/// The web driver.
		/// </value>
		public IWebDriver WebDriver { get; private set; }

		/// <summary>
		/// Initialize the web driver.
		/// </summary>
		/// <param name="webDriverFunc">The function which will return IWebDriver instance.</param>
		/// <returns>IWebDriver instance</returns>
		public IWebDriver UpWebDriver(Func<IWebDriver> webDriverFunc)
		{
			if (webDriverFunc == null) throw new ArgumentNullException(nameof(webDriverFunc));
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				throw new NotSupportedException("Native browser foreground control requires Windows.");
			WebDriver = webDriverFunc() ?? throw new InvalidOperationException("The browser factory returned null.");
			_browserWindowHandle = IntPtr.Zero;
			var javascript = WebDriver as IJavaScriptExecutor ?? throw new NotSupportedException("The browser must support JavaScript execution.");
			string originalTitle = WebDriver.Title;
			string marker = "nvda-testing-driver-" + Guid.NewGuid().ToString("N");
			try
			{
				javascript.ExecuteScript("document.title = arguments[0];", marker);
				var deadline = DateTime.UtcNow.AddSeconds(10);
				while (_browserWindowHandle == IntPtr.Zero && DateTime.UtcNow < deadline)
				{
					_browserWindowHandle = FindTopLevelWindow(marker);
					if (_browserWindowHandle == IntPtr.Zero) Thread.Sleep(200);
				}
			}
			finally
			{
				javascript.ExecuteScript("document.title = arguments[0];", originalTitle);
			}
			if (_browserWindowHandle == IntPtr.Zero)
				throw new InvalidOperationException("Could not find a visible local browser window for this WebDriver session.");
			SetBrowserWindowForeground();
			EnsureBrowserWindowForeground();
			return WebDriver;
		}

		/// <summary>
		/// Gets a value indicating whether the browser window is the foreground window, so keys sent through NVDA reach it.
		/// </summary>
		/// <returns><c>true</c> if the browser window is in the foreground.</returns>
		public bool IsBrowserWindowForeground()
		{
			return _browserWindowHandle != IntPtr.Zero && NativeMethods.IsWindow(_browserWindowHandle) && NativeMethods.GetForegroundWindow() == _browserWindowHandle;
		}

		/// <summary>
		/// Brings the browser window to the foreground, and throws if that is not possible.
		/// Call it before sending keys through NVDA: they are real keystrokes, and go to whatever window is in the foreground.
		/// </summary>
		/// <exception cref="InvalidOperationException">The browser window could not be brought to the foreground.</exception>
		public void EnsureBrowserWindowForeground()
		{
			for (int attempt = 0; attempt < 5 && !IsBrowserWindowForeground(); attempt++)
			{
				ActivateBrowserWindow();
				Thread.Sleep(200);
			}

			if (!IsBrowserWindowForeground())
			{
				throw new InvalidOperationException("The browser window is not in the foreground, so keys sent through NVDA would go to another window. Don't use the machine while the tests run.");
			}
		}

		/// <summary>
		/// Activates the browser window, allowing NVDA to interact with it.
		/// </summary>
		public void SetBrowserWindowForeground()
		{
			WebDriver.Manage().Window.Maximize();
			ActivateBrowserWindow();
		}

		private static IntPtr FindTopLevelWindow(string titleFragment)
		{
			IntPtr found = IntPtr.Zero;
			var title = new StringBuilder(512);
			NativeMethods.EnumWindows(
				(hWnd, lParam) =>
				{
					title.Clear();
					NativeMethods.GetWindowText(hWnd, title, title.Capacity);
					if (NativeMethods.IsWindowVisible(hWnd) && title.ToString().Contains(titleFragment))
					{
						found = hWnd;
						return false;
					}

					return true;
				},
				IntPtr.Zero);
			return found;
		}

		private void ActivateBrowserWindow()
		{
			if (_browserWindowHandle == IntPtr.Zero || !NativeMethods.IsWindow(_browserWindowHandle))
			{
				return;
			}

			// Windows only lets the foreground thread change the foreground window, so share its input state while doing it.
			uint foregroundThread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _);
			uint currentThread = NativeMethods.GetCurrentThreadId();
			bool attached = foregroundThread != currentThread && NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
			try
			{
				NativeMethods.ShowWindow(_browserWindowHandle, NativeMethods.SwShow);
				NativeMethods.BringWindowToTop(_browserWindowHandle);
				NativeMethods.SetForegroundWindow(_browserWindowHandle);
			}
			finally
			{
				if (attached)
				{
					NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
				}
			}
		}

	}
}
