//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using Lookout.Core.Rules;

namespace Lookout.Core
{
	public enum Verdict
	{
		LooksLegit,
		Uncertain,
		Suspicious
	}

	public sealed class Assessment
	{
		public Assessment(string reason, int score)
		{
			Reason = reason;
			Score = score;
		}

		public string Reason { get; }
		public int Score { get; }
	}

	/// <summary>One identity on a message: the Sender or the "on behalf of" identity.</summary>
	public sealed class MailIdentity
	{
		public MailIdentity(string label, string name, string email)
		{
			Label = label;
			Name = name;
			Email = email;
		}

		public string Label { get; }
		public string Name { get; }
		public string Email { get; }
	}

	public static class Assessor
	{
		public const int SuspiciousThreshold = 3;

		public static List<Assessment> AssessIdentity(MailIdentity identity, IEnumerable<SenderRule> rules = null)
		{
			var results = new List<Assessment>();
			foreach (var rule in rules ?? SenderRule.Default)
			{
				if (rule.Matches(identity.Name, identity.Email))
				{
					results.Add(new Assessment(rule.Name, rule.Score));
				}
			}

			return results;
		}

		/// <summary>Flags Sender and On-behalf-of identities whose domains differ.</summary>
		public static List<Assessment> AssessCrossCheck(IReadOnlyList<MailIdentity> identities)
		{
			var results = new List<Assessment>();
			if (identities.Count > 1)
			{
				var a = IdentityChecks.GetDomain(identities[0].Email);
				var b = IdentityChecks.GetDomain(identities[1].Email);

				if (!string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b)
					&& !string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
				{
					results.Add(new Assessment(
						$"Sender domain ({a}) does not match On-behalf-of domain ({b})", 2));
				}
			}

			return results;
		}

		/// <summary>Runs all rules on every identity plus the cross-check and rolls up a verdict.</summary>
		public static (Verdict Verdict, int Score, List<Assessment> Assessments) AssessMessage(
			IReadOnlyList<MailIdentity> identities, IEnumerable<SenderRule> rules = null)
		{
			var all = new List<Assessment>();
			foreach (var identity in identities)
			{
				foreach (var a in AssessIdentity(identity, rules))
				{
					all.Add(new Assessment($"[{identity.Label}] {a.Reason}", a.Score));
				}
			}

			all.AddRange(AssessCrossCheck(identities));

			var score = all.Sum(a => a.Score);
			var verdict = score >= SuspiciousThreshold ? Verdict.Suspicious
				: score > 0 ? Verdict.Uncertain
				: Verdict.LooksLegit;

			return (verdict, score, all);
		}
	}
}
