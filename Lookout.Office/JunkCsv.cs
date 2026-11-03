//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Lookout.Office
{
	/// <summary>
	/// Writes scan results to CSV using the same columns as the LINQPad script's report,
	/// so the two can be diffed. Rows are ordered most severe first: verdict, then score, then newest.
	/// </summary>
	internal static class JunkCsv
	{
		private static readonly string[] Header =
		{
			"EntryId", "Subject", "SenderName", "SenderEmail",
			"OnBehalfOfName", "OnBehalfOfEmail", "Received", "Verdict", "Score", "Reasons"
		};

		public static void Write(string path, IEnumerable<JunkRow> rows)
		{
			var sorted = rows
				.OrderByDescending(r => r.Verdict)
				.ThenByDescending(r => r.Score)
				.ThenByDescending(r => r.Received);

			using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
			{
				writer.WriteLine(string.Join(",", Header));

				foreach (var r in sorted)
				{
					writer.WriteLine(string.Join(",", new[]
					{
						Quote(r.EntryId),
						Quote(r.Subject),
						Quote(r.SenderName),
						Quote(r.SenderEmail),
						Quote(r.OnBehalfOfName),
						Quote(r.OnBehalfOfEmail),
						Quote(r.Received.ToString("yyyy-MM-dd HH:mm:ss")),
						Quote(r.Verdict.ToString()),
						Quote(r.Score.ToString()),
						Quote(r.Reasons)
					}));
				}
			}
		}

		private static string Quote(string value)
		{
			return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
		}
	}
}
