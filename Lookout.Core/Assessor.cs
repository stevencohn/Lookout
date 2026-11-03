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
		/// <param name="reason">Text shown to the user; may include a label prefix or details.</param>
		/// <param name="score">Points this finding contributes.</param>
		/// <param name="key">Stable rule key used by settings; defaults to the reason.</param>
		public Assessment(string reason, int score, string key = null)
		{
			Reason = reason;
			Score = score;
			Key = key ?? reason;
		}

		public string Reason { get; }
		public int Score { get; }
		public string Key { get; }
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
		public const int DefaultSuspiciousThreshold = 3;

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
						$"Sender domain ({a}) does not match On-behalf-of domain ({b})", 2, RuleCatalog.CrossCheck));
				}
			}

			return results;
		}

		/// <summary>
		/// Runs all rules on every identity plus the message-level checks, applies the user's
		/// settings (rules switched off or re-scored) and rolls up a verdict.
		/// </summary>
		public static (Verdict Verdict, int Score, List<Assessment> Assessments) AssessMessage(
			IReadOnlyList<MailIdentity> identities,
			MessageContext context = null,
			RuleSettings settings = null,
			IEnumerable<SenderRule> rules = null)
		{
			settings = settings ?? new RuleSettings();

			var found = new List<Assessment>();
			foreach (var identity in identities)
			{
				foreach (var a in AssessIdentity(identity, rules))
				{
					found.Add(new Assessment($"[{identity.Label}] {a.Reason}", a.Score, a.Key));
				}
			}

			found.AddRange(AssessCrossCheck(identities));

			if (context is not null)
			{
				if (context.Denylist is not null)
				{
					foreach (var identity in identities)
					{
						var entry = context.Denylist.Match(identity);
						if (entry is not null)
						{
							found.Add(new Assessment(
								$"[{identity.Label}] On your denylist ({entry})", 5, RuleCatalog.Denylist));
						}
					}
				}

				if (!string.IsNullOrEmpty(context.Subject))
				{
					foreach (var rule in TextRule.Subject)
					{
						if (rule.Matches(context.Subject))
						{
							found.Add(new Assessment($"[Subject] {rule.Name}", rule.Score, rule.Name));
						}
					}

					if (MessageChecks.SubjectContainsMailboxName(context.Subject, context.MailboxNames))
					{
						found.Add(new Assessment($"[Subject] {RuleCatalog.MailboxName}", 3, RuleCatalog.MailboxName));
					}
				}

				found.AddRange(MessageChecks.AssessAuthentication(context.Headers));
				found.AddRange(MessageChecks.AssessReplyTo(context.Headers, identities));
			}

			var all = found.Select(settings.Apply).Where(a => a is not null).ToList();

			var score = all.Sum(a => a.Score);
			return (VerdictFor(score, settings), score, all);
		}

		/// <summary>Maps a total score to a verdict using the settings' threshold.</summary>
		public static Verdict VerdictFor(int score, RuleSettings settings)
		{
			return score >= settings.SuspiciousThreshold ? Verdict.Suspicious
				: score > 0 ? Verdict.Uncertain
				: Verdict.LooksLegit;
		}
	}
}
