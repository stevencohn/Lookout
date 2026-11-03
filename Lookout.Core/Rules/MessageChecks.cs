//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Lookout.Core.Rules
{
	/// <summary>Everything beyond the sender identities that rules may look at.</summary>
	public sealed class MessageContext
	{
		public string Subject { get; set; }

		/// <summary>Raw internet message headers (PR_TRANSPORT_MESSAGE_HEADERS), if available.</summary>
		public string Headers { get; set; }

		/// <summary>Local parts of the user's own mailbox addresses, e.g. "stevencohn".</summary>
		public IReadOnlyList<string> MailboxNames { get; set; }
	}

	/// <summary>Message-level checks: authentication results and subject personalization.</summary>
	public static class MessageChecks
	{
		private static readonly Regex AuthHeader = new Regex(
			@"^Authentication-Results:(?<value>(?:.*)(?:\r?\n[ \t]+.*)*)",
			RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static readonly Regex AuthResult = new Regex(
			@"\b(?<method>spf|dkim|dmarc)\s*=\s*(?<result>[a-z]+)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled);

		/// <summary>
		/// Reads every Authentication-Results header and reports failed SPF, DKIM or DMARC
		/// checks. A DKIM failure is ignored if another signature passed.
		/// </summary>
		public static List<Assessment> AssessAuthentication(string headers)
		{
			var results = new List<Assessment>();
			if (string.IsNullOrWhiteSpace(headers))
			{
				return results;
			}

			string spf = null, dmarc = null;
			var dkimFail = false;
			var dkimPass = false;

			foreach (Match header in AuthHeader.Matches(headers))
			{
				foreach (Match m in AuthResult.Matches(header.Groups["value"].Value))
				{
					var result = m.Groups["result"].Value.ToLowerInvariant();
					switch (m.Groups["method"].Value.ToLowerInvariant())
					{
						case "spf": spf = spf == "fail" ? spf : result; break;
						case "dmarc": dmarc = dmarc == "fail" ? dmarc : result; break;
						case "dkim":
							dkimFail |= result == "fail";
							dkimPass |= result == "pass";
							break;
					}
				}
			}

			if (dmarc == "fail")
			{
				results.Add(new Assessment("[Headers] DMARC check failed", 3));
			}

			if (spf == "fail")
			{
				results.Add(new Assessment("[Headers] SPF check failed (sending server not authorized)", 2));
			}
			else if (spf == "softfail")
			{
				results.Add(new Assessment("[Headers] SPF soft-failed", 1));
			}

			if (dkimFail && !dkimPass)
			{
				results.Add(new Assessment("[Headers] DKIM signature failed", 1));
			}

			return results;
		}

		/// <summary>
		/// True if the subject contains one of the user's own mailbox names as a whole word,
		/// e.g. "stevencohn Apply Today!" - bulk-mail tools personalize subjects this way.
		/// Names shorter than 5 characters are ignored to avoid common-word matches.
		/// </summary>
		public static bool SubjectContainsMailboxName(string subject, IEnumerable<string> names)
		{
			if (string.IsNullOrEmpty(subject) || names is null)
			{
				return false;
			}

			foreach (var name in names)
			{
				if (!string.IsNullOrEmpty(name) && name.Length >= 5
					&& Regex.IsMatch(subject, $@"(?<![A-Za-z0-9]){Regex.Escape(name)}(?![A-Za-z0-9])", RegexOptions.IgnoreCase))
				{
					return true;
				}
			}

			return false;
		}
	}
}
