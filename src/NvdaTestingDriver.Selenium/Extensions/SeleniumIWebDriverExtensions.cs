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
using OpenQA.Selenium;

namespace NvdaTestingDriver.Selenium.Extensions
{
	/// <summary>
	/// Class to extend IWebDriver with new functionality
	/// </summary>
	public static class SeleniumIWebDriverExtensions
	{
		/// <summary>
		/// Focuses into the specified element.
		/// </summary>
		/// <param name="webDriver">The web driver.</param>
		/// <param name="element">The element.</param>
		/// <exception cref="ArgumentNullException">element</exception>
		public static void Focus(this IWebDriver webDriver, IWebElement element)
		{
			if (webDriver == null) throw new ArgumentNullException(nameof(webDriver));
			if (element == null) throw new ArgumentNullException(nameof(element));
			var javascriptExecutor = webDriver as IJavaScriptExecutor ?? throw new NotSupportedException("The WebDriver must support JavaScript execution.");
			bool focused = (bool)javascriptExecutor.ExecuteScript(@"
arguments[0].focus();
if (document.activeElement !== arguments[0] && !arguments[0].hasAttribute('tabindex')) {
    arguments[0].setAttribute('tabindex', '-1');
    arguments[0].focus();
}
return document.activeElement === arguments[0];", element);
			if (!focused) throw new InvalidOperationException("The element could not receive keyboard focus.");
		}

		/// <summary>Requests focus for the document. Use the wrapper to activate its native window first.</summary>
		/// <param name="webDriver">The browser session.</param>
		public static void FocusOnWindow(this IWebDriver webDriver)
		{
			((IJavaScriptExecutor)webDriver).ExecuteScript("window.focus();");
		}
	}
}