//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System.Linq;
using System.Text.RegularExpressions;

namespace Lookout.Core.Rules
{
	/// <summary>
	/// Pure string checks on a sender display name / email address.
	/// No Outlook dependency so they are unit-testable.
	/// </summary>
	public static class IdentityChecks
	{
		/// <summary>Everything after the last '@', or empty.</summary>
		public static string GetDomain(string email)
		{
			if (string.IsNullOrEmpty(email))
			{
				return string.Empty;
			}

			var at = email.LastIndexOf('@');
			return at >= 0 && at < email.Length - 1 ? email.Substring(at + 1) : string.Empty;
		}

		/// <summary>Everything before the first '@', or empty.</summary>
		public static string GetLocalPart(string email)
		{
			if (string.IsNullOrEmpty(email))
			{
				return string.Empty;
			}

			var at = email.IndexOf('@');
			return at > 0 ? email.Substring(0, at) : string.Empty;
		}

		/// <summary>A domain without any dot cannot resolve on the internet.</summary>
		public static bool DomainHasNoDot(string email)
		{
			var domain = GetDomain(email);
			return !string.IsNullOrEmpty(domain) && !domain.Contains(".");
		}

		/// <summary>RFC 1035/1123: a DNS label must not start or end with a hyphen.</summary>
		public static bool DomainHasInvalidHyphenPlacement(string email)
		{
			var domain = GetDomain(email);
			if (string.IsNullOrEmpty(domain))
			{
				return false;
			}

			foreach (var label in domain.Split('.'))
			{
				if (label.Length > 0 && (label[0] == '-' || label[label.Length - 1] == '-'))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>A non-TLD label made only of digits.</summary>
		public static bool DomainHasAllDigitLabel(string email)
		{
			var domain = GetDomain(email);
			if (string.IsNullOrEmpty(domain))
			{
				return false;
			}

			var labels = domain.Split('.');
			for (var i = 0; i < labels.Length - 1; i++)
			{
				if (labels[i].Length > 0 && labels[i].All(char.IsDigit))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// A non-TLD domain label that looks machine-generated: 8+ letters/digits that mix
		/// lower case, digits and two or more upper-case letters after the first character
		/// (e.g. "7ahYJKxf2u"; real domains are lower case), a long all-caps run with several
		/// digits, or that read as gibberish.
		/// </summary>
		public static bool DomainHasRandomLabel(string email)
		{
			var domain = GetDomain(email);
			if (string.IsNullOrEmpty(domain))
			{
				return false;
			}

			var labels = domain.Split('.');
			for (var i = 0; i < labels.Length - 1; i++)
			{
				if (IsRandomLabel(labels[i].Trim('-')))
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsRandomLabel(string label)
		{
			if (label.Length < 8 || !Regex.IsMatch(label, "^[A-Za-z0-9]+$"))
			{
				return false;
			}

			var hasDigit = label.Any(char.IsDigit);
			var hasLower = label.Any(char.IsLower);
			var upperAfterFirst = label.Skip(1).Count(char.IsUpper);

			// long all-caps run with several digits, e.g. "EJ745A48FU5C0WZB5EUEZ2XZQ8Q7"
			var allCapsWithDigits = !hasLower && label.Length >= 12 && label.Count(char.IsDigit) >= 3;

			return (hasDigit && hasLower && upperAfterFirst >= 2) || allCapsWithDigits || LooksLikeGibberish(label);
		}

		/// <summary>
		/// Unicode Tags block (U+E0000-E007F) and zero-width/format characters
		/// stitched into a name to defeat string-matching filters.
		/// </summary>
		public static bool HasHiddenUnicodeChars(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}

			for (var i = 0; i < text.Length; i++)
			{
				int cp;
				if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
				{
					cp = char.ConvertToUtf32(text[i], text[i + 1]);
					i++;
				}
				else
				{
					cp = text[i];
				}

				if (cp >= 0xE0000 && cp <= 0xE007F)
				{
					return true;
				}

				if (cp == 0x200B || cp == 0x200C || cp == 0x200D || cp == 0xFEFF || cp == 0x00AD)
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Single unbroken token of letters and digits, at least 6 long, with a digit,
		/// an upper-case letter and a vowel ratio below 0.2.
		/// </summary>
		public static bool LooksLikeGibberish(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return false;
			}

			var trimmed = name.Trim();

			if (Regex.IsMatch(trimmed, @"[\s.,\-']") || trimmed.Length < 6
				|| !Regex.IsMatch(trimmed, @"^[A-Za-z0-9]+$"))
			{
				return false;
			}

			var hasDigit = trimmed.Any(char.IsDigit);
			var hasUpper = trimmed.Any(char.IsUpper);
			var vowels = trimmed.Count(c => "AEIOUaeiou".IndexOf(c) >= 0);
			var vowelRatio = (double)vowels / trimmed.Length;

			return hasDigit && hasUpper && vowelRatio < 0.2;
		}

		/// <summary>
		/// A single word mixing Latin letters with Greek or Cyrillic letters, e.g. a Cyrillic
		/// "а" inside "Paypal". Such look-alike characters defeat string-matching filters.
		/// </summary>
		public static bool HasMixedScriptWord(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}

			var latin = false;
			var other = false;

			foreach (var c in text)
			{
				if (char.IsLetter(c))
				{
					if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
					{
						latin = true;
					}
					else if ((c >= 0x0370 && c <= 0x03FF) || (c >= 0x0400 && c <= 0x04FF))
					{
						other = true;
					}
				}
				else
				{
					if (latin && other)
					{
						return true;
					}

					latin = false;
					other = false;
				}
			}

			return latin && other;
		}

		private static readonly Regex WordToken = new Regex(@"[A-Za-z0-9]+", RegexOptions.Compiled);
		private static readonly Regex LookalikeInWord = new Regex(@"[a-z][I01][a-z]", RegexOptions.Compiled);
		private static readonly Regex McMacName = new Regex(@"^(Mc|Mac)[A-Z]", RegexOptions.Compiled);

		/// <summary>
		/// A capital I, zero or one sandwiched between lower-case letters, e.g. "FormuIa" or
		/// "Micr0soft": a letter swapped for a look-alike. Names like McIntosh are excluded.
		/// </summary>
		public static bool HasLookalikeCharacters(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}

			foreach (Match token in WordToken.Matches(text))
			{
				if (!McMacName.IsMatch(token.Value) && LookalikeInWord.IsMatch(token.Value))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>Splits "Display Name &lt;email&gt;" into name and email.</summary>
		public static (string Name, string Email) ParseNameAndEmail(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw))
			{
				return (string.Empty, string.Empty);
			}

			var match = Regex.Match(raw, @"^(?<name>.*?)\s*<(?<email>[^<>]+)>\s*$");
			if (match.Success)
			{
				return (match.Groups["name"].Value.Trim(), match.Groups["email"].Value.Trim());
			}

			return raw.Contains("@")
				? (string.Empty, raw.Trim())
				: (raw.Trim(), string.Empty);
		}
	}
}
