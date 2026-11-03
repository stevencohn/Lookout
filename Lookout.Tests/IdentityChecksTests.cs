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
