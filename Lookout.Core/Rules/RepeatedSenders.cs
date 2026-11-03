//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System;
using System.Collections.Generic;

namespace Lookout.Core.Rules
{
	/// <summary>
	/// Spam runs reuse one display name ("Richard Moore") across many throwaway domains. This
	/// is a scan-level check: it needs every message, not one.
	/// </summary>
	public static class RepeatedSenders
	{
		/// <summary>Distinct registrable domains a display name must come from to be flagged.</summary>
		public const int MinDomains = 3;

		public static string NormalizeName(string name)
		{
			var trimmed = name?.Trim();
			return string.IsNullOrEmpty(trimmed) || trimmed.Length < 3 || trimmed.Contains("@")
				? null
				: trimmed.ToLowerInvariant();
		}

		/// <summary>Normalized display names seen from MinDomains or more different domains.</summary>
		public static HashSet<string> FindNames(IEnumerable<KeyValuePair<string, string>> nameAndEmail)
		{
			var seen = new Dictionary<string, HashSet<string>>();

			foreach (var pair in nameAndEmail)
			{
				var name = NormalizeName(pair.Key);
				var domain = BaseDomain(IdentityChecks.GetDomain(pair.Value));
				if (name is null || domain.Length == 0)
				{
					continue;
				}

				if (!seen.TryGetValue(name, out var domains))
				{
					domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					seen[name] = domains;
				}

				domains.Add(domain);
			}

			var repeated = new HashSet<string>();
			foreach (var entry in seen)
			{
				if (entry.Value.Count >= MinDomains)
				{
					repeated.Add(entry.Key);
				}
			}

			return repeated;
		}

		/// <summary>
		/// Approximates the registrable domain: the last two labels, or three for country-style
		/// suffixes such as co.uk, so mail.example.com and news.example.com count as one.
		/// </summary>
		public static string BaseDomain(string domain)
		{
			if (string.IsNullOrEmpty(domain))
			{
				return string.Empty;
			}

			var labels = domain.Split('.');
			if (labels.Length <= 2)
			{
				return domain;
			}

			var take = labels[labels.Length - 1].Length == 2 && labels[labels.Length - 2].Length <= 3 ? 3 : 2;
			return string.Join(".", labels, labels.Length - take, take);
		}
	}
}
