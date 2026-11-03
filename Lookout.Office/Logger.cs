//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System;
using System.IO;

namespace Lookout.Office
{
	/// <summary>
	/// Appends timestamped lines to %LOCALAPPDATA%\Lookout\lookout.log. Logging must never
	/// break the add-in, so all IO failures are swallowed. The file is trimmed to a fresh
	/// start once it exceeds 1 MB (the previous one is kept as lookout.old.log).
	/// </summary>
	public static class Logger
	{
		private const long MaxBytes = 1024 * 1024;
		private static readonly object gate = new object();

		public static string LogPath { get; } = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"Lookout", "lookout.log");

		public static void Info(string message)
		{
			Write("INFO", message);
		}

		public static void Error(string message, Exception exc = null)
		{
			Write("ERROR", exc is null ? message : $"{message}: {exc}");
		}

		private static void Write(string level, string message)
		{
			try
			{
				lock (gate)
				{
					Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
					Roll();
					File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level} {message}{Environment.NewLine}");
				}
			}
			catch (Exception)
			{
				// never let logging fail the caller
			}
		}

		private static void Roll()
		{
			var info = new FileInfo(LogPath);
			if (info.Exists && info.Length > MaxBytes)
			{
				var old = Path.Combine(info.DirectoryName, "lookout.old.log");
				File.Delete(old);
				File.Move(LogPath, old);
			}
		}
	}
}
