//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Office.Interop.Outlook;

namespace Lookout.Office
{
	/// <summary>
	/// Moves messages to Deleted Items by EntryID. This is a plain move, not a
	/// permanent delete: Outlook.com/M365 mailboxes block permanent delete through
	/// classic MAPI, so retention handles the rest.
	/// </summary>
	public static class JunkMover
	{
		public static int MoveToDeletedItems(
			Microsoft.Office.Interop.Outlook.Application app, IEnumerable<string> entryIds)
		{
			var moved = 0;
			var session = app.Session;

			try
			{
				foreach (var id in entryIds)
				{
					object item = null;
					try
					{
						item = session.GetItemFromID(id);
						if (item is MailItem mail)
						{
							mail.UnRead = false;
							mail.Save(); // persist before Delete
							mail.Delete();
							moved++;
						}
					}
					catch (COMException)
					{
						// leave this item where it is and continue
					}
					finally
					{
						if (item is not null)
						{
							Marshal.ReleaseComObject(item);
						}
					}
				}
			}
			finally
			{
				Marshal.ReleaseComObject(session);
			}

			return moved;
		}
	}
}
