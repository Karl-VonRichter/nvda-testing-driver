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
using System.Runtime.InteropServices;
using System.Text;

namespace NvdaTestingDriver.Selenium
{
	internal static class NativeMethods
	{
		internal const int SwShow = 5;

		internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

		[DllImport("user32.dll")]
		internal static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		internal static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll")]
		internal static extern bool BringWindowToTop(IntPtr hWnd);

		[DllImport("user32.dll")]
		internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

		[DllImport("user32.dll")]
		internal static extern bool IsWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

		[DllImport("kernel32.dll")]
		internal static extern uint GetCurrentThreadId();

		[DllImport("user32.dll")]
		internal static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

		[DllImport("user32.dll")]
		internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

		[DllImport("user32.dll")]
		internal static extern bool IsWindowVisible(IntPtr hWnd);
	}
}
