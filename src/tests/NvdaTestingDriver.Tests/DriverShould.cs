using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvdaTestingDriver.Commands;
using NvdaTestingDriver.Commands.NvdaCommands;
using NvdaTestingDriver.Exceptions;
using NvdaTestingDriver.Settings;

namespace NvdaTestingDriver.Tests
{
	[TestClass]
	public class DriverShould
	{
		[TestMethod]
		public async Task RejectKeysBeforeConnecting()
		{
			using var driver = new NvdaDriver();
			await Assert.ThrowsExactlyAsync<NotConnectedException>(() => driver.SendKeysAsync(Key.Tab));
		}

		[TestMethod]
		public async Task DisconnectAnUnusedDriverRepeatedly()
		{
			using var driver = new NvdaDriver();
			await driver.DisconnectAsync();
			await driver.DisconnectAsync();
		}

		[TestMethod]
		public void SelectTheLaptopCommandIndependentlyOfTheDesktopCommand()
		{
			using var driver = new NvdaDriver(o => o.KeyboardSettings.KeyboardLayout = KeyboardLayout.Laptop);
			var command = new NvdaCommand(new List<KeyCombination>(), new List<KeyCombination> { new KeyCombination { Key.Nvda, Key.L } });
			Assert.AreSame(command.LaptopCombinationSet, driver.GetKeyCombinationSet(command));
		}

		[TestMethod]
		public void RejectAnEmptyLaptopCommand()
		{
			using var driver = new NvdaDriver(o => o.KeyboardSettings.KeyboardLayout = KeyboardLayout.Laptop);
			var command = new NvdaCommand(new List<KeyCombination> { new KeyCombination { Key.Tab } }, new List<KeyCombination>());
			Assert.ThrowsExactly<InvalidCommandException>(() => driver.GetKeyCombinationSet(command));
		}

		[TestMethod]
		public void MoveToThePreviousTableColumnWithTheLeftArrow()
		{
			CollectionAssert.AreEqual(new[] { Key.Control.KeyCode, Key.LeftAlt.KeyCode, Key.LeftArrow.KeyCode }, NavigatingSystemCaretCommands.MoveToPreviousColumn.DesktopCombinationSet[0].Select(key => key.KeyCode).ToArray());
		}

		[TestMethod]
		public void IgnoreMessagesWithoutATypeAndReadSpeechStringsOnly()
		{
			using var driver = new NvdaDriver();
			string speech = null;
			driver.OnSpeakReceived += (_, text) => speech = text;
			driver.ParseMessage("{}");
			driver.ParseMessage("{\"type\":\"speak\",\"sequence\":[\" Name  edit \",{\"command\":\"pitch\"},\"required\"]}");
			Assert.IsTrue(NvdaTestHelper.TextContains(speech, "Name edit required"));
		}

		[TestMethod]
		public void ReceiveSpeechCancellation()
		{
			using var driver = new NvdaDriver();
			bool cancelled = false;
			driver.OnSpeakCancelled += (_, e) => cancelled = true;
			driver.ParseMessage("{\"type\":\"cancel\"}");
			Assert.IsTrue(cancelled);
		}

		[TestMethod]
		public void WriteTheSessionKeyWithoutDebugSpeechLogging()
		{
			string file = Path.GetTempFileName();
			try
			{
				new NvdaDriverOptionsWriter(new NvdaDriverOptions(), "example-session-key").WriteOptionsToIniFile(file);
				string ini = File.ReadAllText(file);
				StringAssert.Contains(ini, "key = example-session-key");
				StringAssert.Contains(ini, "loggingLevel = INFO");
				StringAssert.Contains(ini, "host = 127.0.0.1");
				new NvdaDriverOptionsWriter(new NvdaDriverOptions(), "example-session-key").WriteLegacyRemoteOptionsToIniFile(file);
				string legacy = File.ReadAllText(file);
				StringAssert.Contains(legacy, "key = example-session-key");
				StringAssert.Contains(legacy, "self_hosted = True");
				Assert.IsFalse(legacy.Contains("nvdaremote.com"));
			}
			finally { File.Delete(file); }
		}
	}
}
