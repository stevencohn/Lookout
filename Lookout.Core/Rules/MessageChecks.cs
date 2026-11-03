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

		/// <summary>The user's denylist, if any.</summary>
		public Denylist Denylist { get; set; }
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
				results.Add(new Assessment($"[Headers] {RuleCatalog.DmarcFail}", 3, RuleCatalog.DmarcFail));
			}

			if (spf == "fail")
			{
				results.Add(new Assessment($"[Headers] {RuleCatalog.SpfFail}", 2, RuleCatalog.SpfFail));
			}
			else if (spf == "softfail")
			{
				results.Add(new Assessment($"[Headers] {RuleCatalog.SpfSoftFail}", 1, RuleCatalog.SpfSoftFail));
			}

			if (dkimFail && !dkimPass)
			{
				results.Add(new Assessment($"[Headers] {RuleCatalog.DkimFail}", 1, RuleCatalog.DkimFail));
			}

			return results;
		}

		private static readonly Regex ReplyToHeader = new Regex(
			@"^Reply-To:(?<value>(?:.*)(?:\r?\n[ \t]+.*)*)",
			RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static readonly Regex AddressInHeader = new Regex(
			@"[A-Za-z0-9._%+\-']+@(?<domain>[A-Za-z0-9.\-]+)", RegexOptions.Compiled);

		/// <summary>
		/// Flags a Reply-To address whose domain is unrelated to every sender identity's domain:
		/// replies go somewhere other than where the mail claims to come from.
		/// </summary>
		public static List<Assessment> AssessReplyTo(string headers, IReadOnlyList<MailIdentity> identities)
		{
			var results = new List<Assessment>();
			if (string.IsNullOrWhiteSpace(headers))
			{
				return results;
			}

			var header = ReplyToHeader.Match(headers);
			var address = header.Success ? AddressInHeader.Match(header.Groups["value"].Value) : null;
			if (address is null || !address.Success)
			{
				return results;
			}

			var replyDomain = address.Groups["domain"].Value.TrimEnd('.');
			var known = false;

			foreach (var identity in identities)
			{
				var domain = IdentityChecks.GetDomain(identity.Email);
				if (domain.Length == 0)
				{
					continue;
				}

				known = true;
				if (Related(replyDomain, domain))
				{
					return results;
				}
			}

			if (known)
			{
				results.Add(new Assessment(
					$"[Headers] Reply-To domain ({replyDomain}) differs from the sender's domain", 1, RuleCatalog.ReplyTo));
			}

			return results;
		}

		// the same domain, or one is a subdomain of the other
		private static bool Related(string a, string b)
		{
			return a.Equals(b, StringComparison.OrdinalIgnoreCase)
				|| a.EndsWith("." + b, StringComparison.OrdinalIgnoreCase)
				|| b.EndsWith("." + a, StringComparison.OrdinalIgnoreCase);
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
