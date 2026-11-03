//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

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
