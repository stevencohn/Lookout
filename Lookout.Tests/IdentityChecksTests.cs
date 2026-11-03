//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System.Linq;
using Lookout.Core;
using Lookout.Core.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lookout.Tests
{
	[TestClass]
	public class IdentityChecksTests
	{
		[TestMethod]
		public void HyphenLabelDetected()
		{
			Assert.IsTrue(IdentityChecks.DomainHasInvalidHyphenPlacement("CloudNotify_24@---SyncService-oEDgY.32652322.com"));
			Assert.IsTrue(IdentityChecks.DomainHasInvalidHyphenPlacement("info@-bad.com"));
			Assert.IsFalse(IdentityChecks.DomainHasInvalidHyphenPlacement("a@good-name.com"));
		}

		[TestMethod]
		public void AllDigitLabelDetected()
		{
			Assert.IsTrue(IdentityChecks.DomainHasAllDigitLabel("x@foo.32652322.com"));
			Assert.IsFalse(IdentityChecks.DomainHasAllDigitLabel("x@foo.com"));
			Assert.IsFalse(IdentityChecks.DomainHasAllDigitLabel("x@foo.123"));
		}

		[TestMethod]
		public void NoDotDetected()
		{
			Assert.IsTrue(IdentityChecks.DomainHasNoDot("7IPFEMF6@FNGNOHNG2AL05U46FM9FJTUM29PW"));
			Assert.IsFalse(IdentityChecks.DomainHasNoDot("a@b.com"));
			Assert.IsFalse(IdentityChecks.DomainHasNoDot("no-at-sign"));
		}

		[TestMethod]
		public void HiddenUnicodeDetected()
		{
			Assert.IsTrue(IdentityChecks.HasHiddenUnicodeChars("Dis​ney"));
			Assert.IsTrue(IdentityChecks.HasHiddenUnicodeChars("D" + char.ConvertFromUtf32(0xE0069) + "isney"));
			Assert.IsFalse(IdentityChecks.HasHiddenUnicodeChars("Disney"));
		}

		[TestMethod]
		public void GibberishDetected()
		{
			Assert.IsTrue(IdentityChecks.LooksLikeGibberish("1ASDFVAS83SF8"));
			Assert.IsTrue(IdentityChecks.LooksLikeGibberish("7XPFRMK6"));
			// Ported behavior: "7IPFEMF6" has vowel ratio 0.25 so the original rule does NOT flag it.
			Assert.IsFalse(IdentityChecks.LooksLikeGibberish("7IPFEMF6"));
			Assert.IsFalse(IdentityChecks.LooksLikeGibberish("Steve Cohn"));
			Assert.IsFalse(IdentityChecks.LooksLikeGibberish("Amazon"));
		}

		[TestMethod]
		public void MixedScriptDetected()
		{
			Assert.IsTrue(IdentityChecks.HasMixedScriptWord("Pаypal"));  // Cyrillic a
			Assert.IsTrue(IdentityChecks.HasMixedScriptWord("Hello οnline")); // Greek omicron
			Assert.IsFalse(IdentityChecks.HasMixedScriptWord("Привет world"));
			Assert.IsFalse(IdentityChecks.HasMixedScriptWord("Paypal"));
		}

		[TestMethod]
		public void LookalikeCharactersDetected()
		{
			Assert.IsTrue(IdentityChecks.HasLookalikeCharacters("The 3-in-1 FormuIa That Changed My Sex Life"));
			Assert.IsTrue(IdentityChecks.HasLookalikeCharacters("Micr0soft Support"));
			Assert.IsFalse(IdentityChecks.HasLookalikeCharacters("McIntosh Labs"));
			Assert.IsFalse(IdentityChecks.HasLookalikeCharacters("Steve Cohn"));
			Assert.IsFalse(IdentityChecks.HasLookalikeCharacters("H1N1 update for iPhone 15"));
		}

		[TestMethod]
		public void SubjectLookalikeIsUncertainOnItsOwn()
		{
			var ids = new[] { new MailIdentity("Sender", "Steve Cohn", "steve@example.com") };
			var result = Assessor.AssessMessage(ids, new MessageContext { Subject = "Our new FormuIa" });
			Assert.AreEqual(Verdict.Uncertain, result.Verdict);
			Assert.IsTrue(result.Assessments[0].Reason.StartsWith("[Subject]"));
		}

		[TestMethod]
		public void RandomDomainLabelDetected()
		{
			Assert.IsTrue(IdentityChecks.DomainHasRandomLabel("info@-----mail.7ahYJKxf2u.com"));
			Assert.IsTrue(IdentityChecks.DomainHasRandomLabel("x@---EJ745A48FU5C0WZB5EUEZ2XZQ8Q7.com"));
			Assert.IsFalse(IdentityChecks.DomainHasRandomLabel("noreply@mail.audible.com"));
			Assert.IsFalse(IdentityChecks.DomainHasRandomLabel("ada@e.diabetes.org"));
			Assert.IsFalse(IdentityChecks.DomainHasRandomLabel("a@Fiestaslotspromo.com"));
		}

		[TestMethod]
		public void MailboxNameInSubjectDetected()
		{
			var names = new[] { "stevencohn" };
			Assert.IsTrue(MessageChecks.SubjectContainsMailboxName("stevencohn Apply Today! Get up to $5,000!", names));
			Assert.IsTrue(MessageChecks.SubjectContainsMailboxName("Hi StevenCohn, your order", names));
			Assert.IsFalse(MessageChecks.SubjectContainsMailboxName("Your order has shipped", names));
			Assert.IsFalse(MessageChecks.SubjectContainsMailboxName("xstevencohnx", names));
		}

		[TestMethod]
		public void FailedAuthenticationDetected()
		{
			var headers = "Received: from x\r\nAuthentication-Results: spf=fail (sender IP is 1.2.3.4)\r\n smtp.mailfrom=bad.com;\r\n dkim=none; dmarc=fail action=none header.from=bad.com;\r\nSubject: hi\r\n";
			var results = MessageChecks.AssessAuthentication(headers);
			Assert.AreEqual(2, results.Count);
			Assert.AreEqual(5, results.Sum(a => a.Score));

			var good = "Authentication-Results: spf=pass smtp.mailfrom=a.com; dkim=pass header.d=a.com; dmarc=pass header.from=a.com\r\n";
			Assert.AreEqual(0, MessageChecks.AssessAuthentication(good).Count);

			Assert.AreEqual(0, MessageChecks.AssessAuthentication(null).Count);
		}

		private static readonly MailIdentity[] HyphenSender =
		{
			new MailIdentity("Sender", "Steve Cohn", "steve@-bad.com")
		};

		[TestMethod]
		public void DisabledRuleIsIgnored()
		{
			Assert.AreEqual(Verdict.Suspicious, Assessor.AssessMessage(HyphenSender).Verdict);

			var settings = new RuleSettings();
			settings.Rules["Domain label starts or ends with a hyphen (invalid DNS label syntax)"] =
				new RuleOverride { Enabled = false };

			var result = Assessor.AssessMessage(HyphenSender, null, settings);
			Assert.AreEqual(Verdict.LooksLegit, result.Verdict);
			Assert.AreEqual(0, result.Assessments.Count);
		}

		[TestMethod]
		public void ScoreOverrideAndThresholdChangeVerdict()
		{
			var settings = new RuleSettings();
			settings.Rules["Domain label starts or ends with a hyphen (invalid DNS label syntax)"] =
				new RuleOverride { Score = 1 };
			Assert.AreEqual(Verdict.Uncertain, Assessor.AssessMessage(HyphenSender, null, settings).Verdict);

			var strict = new RuleSettings { SuspiciousThreshold = 5 };
			Assert.AreEqual(Verdict.Uncertain, Assessor.AssessMessage(HyphenSender, null, strict).Verdict);
		}

		[TestMethod]
		public void HeaderRuleCanBeDisabledByKey()
		{
			var ids = new[] { new MailIdentity("Sender", "Steve Cohn", "steve@example.com") };
			var context = new MessageContext { Headers = "Authentication-Results: dmarc=fail header.from=x.com\r\n" };

			Assert.AreEqual(Verdict.Suspicious, Assessor.AssessMessage(ids, context).Verdict);

			var settings = new RuleSettings();
			settings.Rules[RuleCatalog.DmarcFail] = new RuleOverride { Enabled = false };
			Assert.AreEqual(Verdict.LooksLegit, Assessor.AssessMessage(ids, context, settings).Verdict);
		}

		[TestMethod]
		public void SettingsRoundTripAndOnlyWriteChanges()
		{
			var settings = new RuleSettings { SuspiciousThreshold = 4, ScanOnOpen = false };
			settings.Rules["Rule A"] = new RuleOverride { Enabled = false };
			settings.Rules["Rule B"] = new RuleOverride { Score = 7 };
			settings.Rules["Rule C"] = new RuleOverride(); // default: not written

			var text = settings.ToText();
			Assert.IsFalse(text.Contains("Rule C"));

			var parsed = RuleSettings.Parse(text);
			Assert.AreEqual(4, parsed.SuspiciousThreshold);
			Assert.IsFalse(parsed.ScanOnOpen);
			Assert.IsFalse(parsed.Rules["Rule A"].Enabled);
			Assert.AreEqual(7, parsed.Rules["Rule B"].Score);
			Assert.IsFalse(parsed.Rules.ContainsKey("Rule C"));
		}

		[TestMethod]
		public void BadSettingsTextFallsBackToDefaults()
		{
			var parsed = RuleSettings.Parse("garbage\nSuspiciousThreshold=-2\nScanOnOpen=maybe\nRule=oops\n");
			Assert.AreEqual(Assessor.DefaultSuspiciousThreshold, parsed.SuspiciousThreshold);
			Assert.IsTrue(parsed.ScanOnOpen);
			Assert.AreEqual(0, parsed.Rules.Count);
		}

		[TestMethod]
		public void CatalogCoversEveryProducedRuleKey()
		{
			var keys = RuleCatalog.All.Select(r => r.Key).ToList();
			Assert.AreEqual(keys.Count, keys.Distinct().Count(), "duplicate rule keys");
			Assert.IsTrue(keys.Contains(RuleCatalog.CrossCheck));
			Assert.IsTrue(keys.Contains(RuleCatalog.DkimFail));
			Assert.IsTrue(SenderRule.Default.All(r => keys.Contains(r.Name)));
			Assert.IsTrue(TextRule.Subject.All(r => keys.Contains(r.Name)));
		}

		[TestMethod]
		public void ParseNameAndEmail()
		{
			Assert.AreEqual(("Cloud NOTICE", "a@b"), IdentityChecks.ParseNameAndEmail("Cloud NOTICE <a@b>"));
			Assert.AreEqual(("", "a@b.com"), IdentityChecks.ParseNameAndEmail("a@b.com"));
			Assert.AreEqual(("Name", ""), IdentityChecks.ParseNameAndEmail("Name"));
		}

		[TestMethod]
		public void SpoofedOnBehalfOfIsSuspicious()
		{
			var ids = new[]
			{
				new MailIdentity("Sender", "Fiestaslots", "noreply@Fiestaslotspromo.com"),
				new MailIdentity("On behalf of", "Cloud NOTICE", "7IPFEMF6@FNGNOHNG2AL05U46FM9FJTUM29PW"),
			};

			Assert.AreEqual(Verdict.Suspicious, Assessor.AssessMessage(ids).Verdict);
		}

		[TestMethod]
		public void NormalSenderLooksLegit()
		{
			var ids = new[] { new MailIdentity("Sender", "Steve Cohn", "steve@example.com") };
			Assert.AreEqual(Verdict.LooksLegit, Assessor.AssessMessage(ids).Verdict);
		}
	}
}
