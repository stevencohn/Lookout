//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System;
using System.IO;
using Lookout.Core;

namespace Lookout.Office
{
	/// <summary>Loads and saves RuleSettings at %LOCALAPPDATA%\Lookout\settings.conf.</summary>
	public static class SettingsStore
	{
		public static string SettingsPath { get; } = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"Lookout", "settings.conf");

		public static string DenylistPath { get; } = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"Lookout", "denylist.txt");

		/// <summary>The user's denylist; empty if the file is missing or unreadable.</summary>
		public static Denylist LoadDenylist()
		{
			try
			{
				return File.Exists(DenylistPath)
					? Denylist.Parse(File.ReadAllText(DenylistPath))
					: new Denylist();
			}
			catch (Exception exc) when (exc is IOException || exc is UnauthorizedAccessException)
			{
				Logger.Error("Could not read denylist", exc);
				return new Denylist();
			}
		}

		/// <summary>Creates the denylist file with instructions if needed and opens it in Notepad.</summary>
		public static void OpenDenylist()
		{
			try
			{
				if (!File.Exists(DenylistPath))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(DenylistPath));
					File.WriteAllText(DenylistPath,
						"# Lookout denylist: senders to always treat as junk. One entry per line.\r\n" +
						"#\r\n" +
						"#   badsender.com        a domain; also matches its subdomains\r\n" +
						"#   name:Richard Moore   a display name (ignoring case)\r\n" +
						"#\r\n" +
						"# Lines starting with # are ignored. Save the file, then rescan.\r\n");
				}

				System.Diagnostics.Process.Start("notepad.exe", "\"" + DenylistPath + "\"");
			}
			catch (Exception exc) when (exc is IOException || exc is UnauthorizedAccessException
				|| exc is System.ComponentModel.Win32Exception)
			{
				Logger.Error("Could not open denylist", exc);
			}
		}

		/// <summary>Returns the saved settings, or defaults if the file is missing or unreadable.</summary>
		public static RuleSettings Load()
		{
			try
			{
				return File.Exists(SettingsPath)
					? RuleSettings.Parse(File.ReadAllText(SettingsPath))
					: new RuleSettings();
			}
			catch (Exception exc) when (exc is IOException || exc is UnauthorizedAccessException)
			{
				Logger.Error("Could not read settings; using defaults", exc);
				return new RuleSettings();
			}
		}

		public static bool Save(RuleSettings settings)
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
				File.WriteAllText(SettingsPath, settings.ToText());
				Logger.Info("Settings saved");
				return true;
			}
			catch (Exception exc) when (exc is IOException || exc is UnauthorizedAccessException)
			{
				Logger.Error("Could not save settings", exc);
				return false;
			}
		}
	}
}
