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
