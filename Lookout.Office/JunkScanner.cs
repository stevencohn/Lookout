//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Lookout.Core;
using Lookout.Core.Rules;
using Microsoft.Office.Interop.Outlook;

namespace Lookout.Office
{
	/// <summary>
	/// Reads the Junk Email folder in batches so the caller can stay on the Outlook
	/// UI thread (the object model is not thread-safe) without freezing it.
	/// </summary>
	public sealed class JunkScanner : IDisposable
	{
		// PR_SENT_REPRESENTING_EMAIL_ADDRESS: the Object Model has no
		// SentOnBehalfOfEmailAddress, so it has to be read via PropertyAccessor.
		private const string PrSentRepresentingEmail =
			"http://schemas.microsoft.com/mapi/proptag/0x0065001E";

		// PR_TRANSPORT_MESSAGE_HEADERS: the raw internet headers, including
		// Authentication-Results (SPF/DKIM/DMARC).
		private const string PrTransportHeaders =
			"http://schemas.microsoft.com/mapi/proptag/0x007D001E";

		private MAPIFolder junk;
		private Items items;
		private int next = 1;
		private readonly List<string> mailboxNames = new List<string>();

		private readonly RuleSettings settings;

		public JunkScanner(Microsoft.Office.Interop.Outlook.Application app, RuleSettings settings)
		{
			this.settings = settings;
			var session = app.Session;
			try
			{
				junk = session.GetDefaultFolder(OlDefaultFolders.olFolderJunk);
				items = junk.Items;
				Total = items.Count;
				LoadMailboxNames(session);
			}
			finally
			{
				Marshal.ReleaseComObject(session);
			}
		}

		/// <summary>Local parts of every account's SMTP address, lower-cased.</summary>
		private void LoadMailboxNames(NameSpace session)
		{
			var accounts = session.Accounts;
			try
			{
				foreach (Account account in accounts)
				{
					try
					{
						var smtp = account.SmtpAddress;
						var at = smtp?.IndexOf('@') ?? -1;
						if (at > 0)
						{
							mailboxNames.Add(smtp.Substring(0, at).ToLowerInvariant());
						}
					}
					catch (COMException)
					{
						// account without an SMTP address
					}
					finally
					{
						Marshal.ReleaseComObject(account);
					}
				}
			}
			finally
			{
				Marshal.ReleaseComObject(accounts);
			}
		}

		public int Total { get; }
		public int Scanned => next - 1;
		public bool Done => next > Total;

		/// <summary>Scans up to batchSize more items and returns rows for the mail items found.</summary>
		public List<JunkRow> ReadBatch(int batchSize)
		{
			var rows = new List<JunkRow>();

			for (var n = 0; n < batchSize && next <= Total; n++, next++)
			{
				object item = null;
				try
				{
					item = items[next];
					if (item is MailItem mail)
					{
						rows.Add(Assess(mail));
					}
				}
				catch (COMException)
				{
					// item vanished or is unreadable; skip it
				}
				finally
				{
					if (item is not null)
					{
						Marshal.ReleaseComObject(item);
					}
				}
			}

			return rows;
		}

		private JunkRow Assess(MailItem mail)
		{
			var identities = GetIdentities(mail);
			var result = Assessor.AssessMessage(identities, new MessageContext
			{
				Subject = mail.Subject,
				Headers = GetHeaders(mail),
				MailboxNames = mailboxNames
			}, settings);
			var onBehalfOf = identities.Count > 1 ? identities[1] : null;

			return new JunkRow
			{
				EntryId = mail.EntryID,
				Subject = mail.Subject,
				SenderName = mail.SenderName,
				SenderEmail = mail.SenderEmailAddress,
				OnBehalfOfName = onBehalfOf?.Name ?? string.Empty,
				OnBehalfOfEmail = onBehalfOf?.Email ?? string.Empty,
				Received = mail.ReceivedTime,
				Verdict = result.Verdict,
				Score = result.Score,
				Reasons = string.Join("; ", result.Assessments.Select(a => a.Reason))
			};
		}

		/// <summary>
		/// The actual Sender, plus the claimed "on behalf of" identity when it is
		/// present and different.
		/// </summary>
		private static List<MailIdentity> GetIdentities(MailItem mail)
		{
			var identities = new List<MailIdentity>
			{
				new MailIdentity("Sender", mail.SenderName, mail.SenderEmailAddress)
			};

			var onBehalfOfName = mail.SentOnBehalfOfName;
			if (!string.IsNullOrWhiteSpace(onBehalfOfName)
				&& !string.Equals(onBehalfOfName, mail.SenderName, StringComparison.OrdinalIgnoreCase))
			{
				var parsed = IdentityChecks.ParseNameAndEmail(onBehalfOfName);
				var email = string.IsNullOrEmpty(parsed.Email) ? GetOnBehalfOfEmail(mail) : parsed.Email;
				identities.Add(new MailIdentity("On behalf of", parsed.Name, email));
			}

			return identities;
		}

		private static string GetHeaders(MailItem mail)
		{
			var accessor = mail.PropertyAccessor;
			try
			{
				return accessor.GetProperty(PrTransportHeaders) as string ?? string.Empty;
			}
			catch (COMException)
			{
				return string.Empty;
			}
			finally
			{
				Marshal.ReleaseComObject(accessor);
			}
		}

		private static string GetOnBehalfOfEmail(MailItem mail)
		{
			var accessor = mail.PropertyAccessor;
			try
			{
				return accessor.GetProperty(PrSentRepresentingEmail) as string ?? string.Empty;
			}
			catch (COMException)
			{
				return string.Empty;
			}
			finally
			{
				Marshal.ReleaseComObject(accessor);
			}
		}

		public void Dispose()
		{
			if (items is not null)
			{
				Marshal.ReleaseComObject(items);
				items = null;
			}

			if (junk is not null)
			{
				Marshal.ReleaseComObject(junk);
				junk = null;
			}
		}
	}
}
