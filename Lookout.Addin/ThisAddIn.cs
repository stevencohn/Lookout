//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using Lookout.Office;
using Microsoft.Office.Tools;

namespace Lookout.Addin
{
	public partial class ThisAddIn
	{
		private CustomTaskPane pane;
		private ReviewPane reviewPane;

		protected override Microsoft.Office.Core.IRibbonExtensibility CreateRibbonExtensibilityObject()
		{
			return new JunkRibbon(TogglePane);
		}

		private void TogglePane()
		{
			if (pane is null)
			{
				reviewPane = new ReviewPane(Application);
				pane = CustomTaskPanes.Add(reviewPane, "Lookout");
				pane.Width = 600;
			}

			pane.Visible = !pane.Visible;

			if (pane.Visible && SettingsStore.Load().ScanOnOpen)
			{
				reviewPane.ScanIfIdle();
			}
		}

		private void ThisAddIn_Startup(object sender, System.EventArgs e)
		{
		}

		private void ThisAddIn_Shutdown(object sender, System.EventArgs e)
		{
		}

		private void InternalStartup()
		{
			Startup += ThisAddIn_Startup;
			Shutdown += ThisAddIn_Shutdown;
		}
	}
}