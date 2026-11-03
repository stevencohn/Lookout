# Lookout

Outlook (classic, VSTO) add-in that reviews the Junk Email folder, flags messages with suspicious sender
identities, and lets you move the ones you choose to Deleted Items. Ported from
`Office-outlook-junk-review.linq`.

Not supported: new Outlook for Windows and Outlook on the web (they do not load VSTO/COM add-ins).

## Projects

| Project | Target | Role |
|---------|--------|------|
| `Lookout.Core` | netstandard2.0 | Rules, scoring, parsing. No Outlook dependency. |
| `Lookout.Tests` | net8.0 | MSTest unit tests for Core. |
| `Lookout.Office` | net48 | Scanner, mover, ribbon, review pane (uses the Outlook PIA). |
| `Lookout.Addin` | net48 VSTO | Thin shell: `ThisAddIn` only. **Create in Visual Studio** (see below). |

## Creating the VSTO shell (one-time)

The VSTO project's generated files (`ThisAddIn.Designer.cs`, manifests) come from the Visual Studio template.

1. In Visual Studio (Office/SharePoint development workload): File > Add > New Project >
   **Outlook VSTO Add-in** (C#), name `Lookout.Addin`, location this folder, .NET Framework 4.8.
2. Add project references to `Lookout.Office` (and `Lookout.Core`).
3. Replace the generated `ThisAddIn.cs` with:

```csharp
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

		protected override Microsoft.Office.Core.IRibbonExtensibility CreateRibbonExtensibilityObject()
		{
			return new JunkRibbon(TogglePane);
		}

		private void TogglePane()
		{
			if (pane is null)
			{
				pane = CustomTaskPanes.Add(new ReviewPane(Application), "Lookout");
				pane.Width = 600;
			}

			pane.Visible = !pane.Visible;
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
```

4. Press F5: Outlook starts with a **Lookout > Review Junk** button on the Mail ribbon.

## Behavior

Review Junk opens the pane. Scan reads Junk in batches of 25 on the UI thread. Suspicious rows are pre-checked.
Nothing moves until you click **Move selected to Deleted Items** and confirm. It is a move, not a permanent delete:
Outlook.com/M365 mailboxes block permanent delete through classic MAPI.

## Adding a rule

Add an entry to `SenderRule.Default` in `Lookout.Core/Rules/SenderRule.cs`, backed by a check in `IdentityChecks`,
and add a test.

## Tests

```powershell
dotnet test
```
