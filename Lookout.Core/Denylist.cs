//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System;
using System.Collections.Generic;

namespace Lookout.Core
{
	/// <summary>
	/// A user-maintained list of senders to always treat as junk. Stored one entry per line:
	/// <code>
	/// # comment
	/// badsender.com          a domain; also matches its subdomains (mail.badsender.com)
	/// name:Richard Moore     a display name, compared ignoring case
	/// </code>
	/// </summary>
	public sealed class Denylist
	{
		private readonly HashSet<string> domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		public bool IsEmpty => domains.Count == 0 && names.Count == 0;

		public static Denylist Parse(string text)
		{
			var list = new Denylist();
			if (string.IsNullOrEmpty(text))
			{
				return list;
			}

			foreach (var raw in text.Split('\n'))
			{
				var line = raw.Trim();
				if (line.Length == 0 || line[0] == '#')
				{
					continue;
				}

				if (line.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
				{
					var name = line.Substring(5).Trim();
					if (name.Length > 0)
					{
						list.names[name] = "name:" + name;
					}
				}
				else
				{
					var domain = line.TrimStart('@', '*', '.').Trim();
					if (domain.Length > 0)
					{
						list.domains.Add(domain);
					}
				}
			}

			return list;
		}

		/// <summary>The matching entry for this identity, or null.</summary>
		public string Match(MailIdentity identity)
		{
			if (IsEmpty)
			{
				return null;
			}

			var name = identity.Name?.Trim();
			if (!string.IsNullOrEmpty(name) && names.TryGetValue(name, out var entry))
			{
				return entry;
			}

			var domain = Rules.IdentityChecks.GetDomain(identity.Email);
			if (domain.Length > 0)
			{
				foreach (var listed in domains)
				{
					if (domain.Equals(listed, StringComparison.OrdinalIgnoreCase)
						|| domain.EndsWith("." + listed, StringComparison.OrdinalIgnoreCase))
					{
						return listed;
					}
				}
			}

			return null;
		}
	}
}
