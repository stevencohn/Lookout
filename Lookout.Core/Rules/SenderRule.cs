//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System;
using System.Collections.Generic;

namespace Lookout.Core.Rules
{
	public sealed class SenderRule
	{
		public SenderRule(string name, int score, Func<string, string, bool> matches)
		{
			Name = name;
			Score = score;
			Matches = matches;
		}

		public string Name { get; }
		public int Score { get; }
		public Func<string, string, bool> Matches { get; }

		/// <summary>
		/// The rule collection. Add an entry here to teach Lookout a new red flag.
		/// </summary>
		public static IReadOnlyList<SenderRule> Default { get; } = new List<SenderRule>
		{
			new SenderRule(
				"Display name contains hidden/invisible Unicode characters", 3,
				(name, email) => IdentityChecks.HasHiddenUnicodeChars(name)),

			new SenderRule(
				"Display name looks like random gibberish, not a real name", 2,
				(name, email) => IdentityChecks.LooksLikeGibberish(name)),

			new SenderRule(
				"Domain label starts or ends with a hyphen (invalid DNS label syntax)", 4,
				(name, email) => IdentityChecks.DomainHasInvalidHyphenPlacement(email)),

			new SenderRule(
				"Domain has an all-numeric label (not a registered-looking domain)", 2,
				(name, email) => IdentityChecks.DomainHasAllDigitLabel(email)),

			new SenderRule(
				"Domain has no dot at all - not a resolvable internet domain", 4,
				(name, email) => IdentityChecks.DomainHasNoDot(email)),

			new SenderRule(
				"Email local-part looks like random gibberish, not a real mailbox name", 2,
				(name, email) => IdentityChecks.LooksLikeGibberish(IdentityChecks.GetLocalPart(email))),

			new SenderRule(
				"Domain has a random-looking label (machine-generated hostname)", 3,
				(name, email) => IdentityChecks.DomainHasRandomLabel(email)),

			new SenderRule(
				"Display name mixes Latin with Greek/Cyrillic look-alike letters", 3,
				(name, email) => IdentityChecks.HasMixedScriptWord(name)),

			new SenderRule(
				"Display name has a look-alike character inside a word (I, 0 or 1 for a letter)", 2,
				(name, email) => IdentityChecks.HasLookalikeCharacters(name)),
		};
	}
}
