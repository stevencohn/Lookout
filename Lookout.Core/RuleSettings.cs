//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Lookout.Core
{
	/// <summary>A user override of one rule: switched off, and/or a different score.</summary>
	public sealed class RuleOverride
	{
		public bool Enabled { get; set; } = true;
		public int? Score { get; set; }
	}

	/// <summary>
	/// User-tunable settings. Stored as a small hand-editable text file:
	/// <code>
	/// SuspiciousThreshold=3
	/// ScanOnOpen=true
	/// Rule=0|-|Domain has an all-numeric label (not a registered-looking domain)
	/// Rule=1|5|SPF check failed (sending server not authorized)
	/// </code>
	/// A Rule line is enabled(1/0) | score or '-' for the default | rule key. Only rules that
	/// differ from their defaults are written.
	/// </summary>
	public sealed class RuleSettings
	{
		public int SuspiciousThreshold { get; set; } = Assessor.DefaultSuspiciousThreshold;
		public bool ScanOnOpen { get; set; } = true;
		public Dictionary<string, RuleOverride> Rules { get; } =
			new Dictionary<string, RuleOverride>(StringComparer.Ordinal);

		/// <summary>Applies an override to an assessment; null if its rule is switched off.</summary>
		public Assessment Apply(Assessment assessment)
		{
			if (Rules.TryGetValue(assessment.Key, out var rule))
			{
				if (!rule.Enabled)
				{
					return null;
				}

				if (rule.Score.HasValue)
				{
					return new Assessment(assessment.Reason, rule.Score.Value, assessment.Key);
				}
			}

			return assessment;
		}

		public static RuleSettings Parse(string text)
		{
			var settings = new RuleSettings();
			if (string.IsNullOrEmpty(text))
			{
				return settings;
			}

			foreach (var raw in text.Split('\n'))
			{
				var line = raw.Trim();
				if (line.Length == 0 || line[0] == '#')
				{
					continue;
				}

				var eq = line.IndexOf('=');
				if (eq <= 0)
				{
					continue;
				}

				var name = line.Substring(0, eq).Trim();
				var value = line.Substring(eq + 1).Trim();

				switch (name)
				{
					case "SuspiciousThreshold":
						if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var threshold) && threshold > 0)
						{
							settings.SuspiciousThreshold = threshold;
						}
						break;

					case "ScanOnOpen":
						if (bool.TryParse(value, out var scan))
						{
							settings.ScanOnOpen = scan;
						}
						break;

					case "Rule":
						ParseRule(settings, value);
						break;
				}
			}

			return settings;
		}

		private static void ParseRule(RuleSettings settings, string value)
		{
			var first = value.IndexOf('|');
			var second = first < 0 ? -1 : value.IndexOf('|', first + 1);
			if (second < 0)
			{
				return;
			}

			var key = value.Substring(second + 1).Trim();
			var scoreText = value.Substring(first + 1, second - first - 1).Trim();

			var rule = new RuleOverride { Enabled = value.Substring(0, first).Trim() != "0" };
			if (int.TryParse(scoreText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var score) && score >= 0)
			{
				rule.Score = score;
			}

			if (key.Length > 0)
			{
				settings.Rules[key] = rule;
			}
		}

		public string ToText()
		{
			var text = new StringBuilder();
			text.Append("# Lookout settings. Only rules that differ from their defaults are listed.\n");
			text.Append("SuspiciousThreshold=").Append(SuspiciousThreshold.ToString(CultureInfo.InvariantCulture)).Append('\n');
			text.Append("ScanOnOpen=").Append(ScanOnOpen ? "true" : "false").Append('\n');

			foreach (var pair in Rules)
			{
				if (pair.Value.Enabled && !pair.Value.Score.HasValue)
				{
					continue;
				}

				text.Append("Rule=").Append(pair.Value.Enabled ? '1' : '0').Append('|')
					.Append(pair.Value.Score.HasValue ? pair.Value.Score.Value.ToString(CultureInfo.InvariantCulture) : "-")
					.Append('|').Append(pair.Key).Append('\n');
			}

			return text.ToString();
		}
	}
}
