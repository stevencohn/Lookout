//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System.Collections.Generic;

namespace Lookout.Core.Rules
{
	/// <summary>One tunable rule: its stable key, group for display, and default score.</summary>
	public sealed class RuleInfo
	{
		public RuleInfo(string key, string group, int defaultScore)
		{
			Key = key;
			Group = group;
			DefaultScore = defaultScore;
		}

		public string Key { get; }
		public string Group { get; }
		public int DefaultScore { get; }
	}

	/// <summary>
	/// Every rule the user can tune. A rule's key is its description text; settings are
	/// stored against it. Sender and subject rules come from their rule lists, the rest
	/// are the message-level checks that Assessor and MessageChecks produce.
	/// </summary>
	public static class RuleCatalog
	{
		public const string CrossCheck = "Sender and On-behalf-of domains differ";
		public const string MailboxName = "Subject contains your own mailbox name (bulk-mail personalization)";
		public const string DmarcFail = "DMARC check failed";
		public const string SpfFail = "SPF check failed (sending server not authorized)";
		public const string SpfSoftFail = "SPF soft-failed";
		public const string DkimFail = "DKIM signature failed";
		public const string ReplyTo = "Reply-To domain differs from the sender's domain";
		public const string Denylist = "Sender is on your denylist";
		public const string RepeatedSender = "Same display name sent from several unrelated domains";

		public static IReadOnlyList<RuleInfo> All { get; } = Build();

		private static List<RuleInfo> Build()
		{
			var all = new List<RuleInfo>();

			foreach (var rule in SenderRule.Default)
			{
				all.Add(new RuleInfo(rule.Name, "Sender", rule.Score));
			}

			all.Add(new RuleInfo(CrossCheck, "Sender", 2));
			all.Add(new RuleInfo(Denylist, "Sender", 5));
			all.Add(new RuleInfo(RepeatedSender, "Sender", 2));

			foreach (var rule in TextRule.Subject)
			{
				all.Add(new RuleInfo(rule.Name, "Subject", rule.Score));
			}

			all.Add(new RuleInfo(MailboxName, "Subject", 3));
			all.Add(new RuleInfo(DmarcFail, "Headers", 3));
			all.Add(new RuleInfo(SpfFail, "Headers", 2));
			all.Add(new RuleInfo(SpfSoftFail, "Headers", 1));
			all.Add(new RuleInfo(DkimFail, "Headers", 1));
			all.Add(new RuleInfo(ReplyTo, "Headers", 1));

			return all;
		}
	}
}
