//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Office.Core;

namespace Lookout.Office
{
	/// <summary>
	/// Ribbon on the Mail explorer. The add-in host returns an instance from
	/// CreateRibbonExtensibilityObject.
	/// </summary>
	[ComVisible(true)]
	public class JunkRibbon : IRibbonExtensibility
	{
		private readonly Action toggleReviewPane;
		private IRibbonUI ribbon;

		public JunkRibbon(Action toggleReviewPane)
		{
			this.toggleReviewPane = toggleReviewPane;
		}

		public string GetCustomUI(string ribbonId)
		{
			// the same ribbon XML is used for the Mail explorer only
			if (ribbonId != "Microsoft.Outlook.Explorer")
			{
				return null;
			}

			var assembly = Assembly.GetExecutingAssembly();
			using (var stream = assembly.GetManifestResourceStream("Lookout.Office.JunkRibbon.xml"))
			using (var reader = new StreamReader(stream))
			{
				return reader.ReadToEnd();
			}
		}

		// callbacks are bound by name from the ribbon XML

		public void Ribbon_Load(IRibbonUI ribbonUI)
		{
			ribbon = ribbonUI;
		}

		public void OnReviewJunk(IRibbonControl control)
		{
			toggleReviewPane();
		}
	}
}
