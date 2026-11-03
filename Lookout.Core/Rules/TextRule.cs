//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System;
using System.Collections.Generic;

namespace Lookout.Core.Rules
{
	/// <summary>A rule over a single piece of free text, such as the message subject.</summary>
	public sealed class TextRule
	{
		public TextRule(string name, int score, Func<string, bool> matches)
		{
			Name = name;
			Score = score;
			Matches = matches;
		}

		public string Name { get; }
		public int Score { get; }
		public Func<string, bool> Matches { get; }

		/// <summary>Rules applied to the message subject.</summary>
		public static IReadOnlyList<TextRule> Subject { get; } = new List<TextRule>
		{
			new TextRule(
				"Subject mixes Latin with Greek/Cyrillic look-alike letters", 3,
				IdentityChecks.HasMixedScriptWord),

			new TextRule(
				"Subject has a look-alike character inside a word (I, 0 or 1 for a letter)", 2,
				IdentityChecks.HasLookalikeCharacters),
		};
	}
}
