//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System;
using Lookout.Core;

namespace Lookout.Office
{
	/// <summary>One scanned Junk message with its verdict.</summary>
	public sealed class JunkRow
	{
		public string EntryId { get; set; }
		public string Subject { get; set; }
		public string SenderName { get; set; }
		public string SenderEmail { get; set; }
		public string OnBehalfOfName { get; set; }
		public string OnBehalfOfEmail { get; set; }
		public DateTime Received { get; set; }
		public Verdict Verdict { get; set; }
		public int Score { get; set; }
		public string Reasons { get; set; }
	}
}
