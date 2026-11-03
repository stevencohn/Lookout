# Lookout: context for Claude

A classic-Outlook (VSTO) add-in that reviews the Junk Email folder, scores each message's sender identity,
subject and headers against a list of red flags, and lets the user delete the flagged ones (a move to Deleted
Items). Ported from a LINQPad script. See `README.md` for users and the rule reference.

## Stack

- **VSTO add-in on .NET Framework 4.8**, C# LangVersion 9.0. VSTO does not support .NET Core / .NET 5+, and the
  new Outlook and OWA do not load it.
- Windows-only. Uses the Office 2013 (Office15) PIAs from the Visual Studio shared folder.
- WiX 7 for the MSI. Tests are MSTest on net8.0.

## Building

Use `.\build.ps1` from the repo root in PowerShell. Do **not** use `dotnet build` on the solution: the VSTO
project needs Visual Studio's MSBuild, and the installer needs the manifest-signing public key that `build.ps1`
extracts. `dotnet test Lookout.Tests` is fine on its own. Close Outlook before building; it locks the DLLs.

Each build of `Lookout.Addin` registers the add-in under `HKCU\...\Outlook\Addins\Lookout.Addin` pointing at its
`bin` folder, so Outlook loads the latest build whenever it starts. That key beats an installed MSI.

## Projects

| Project | Role |
|---------|------|
| `Lookout.Core` (netstandard2.0) | Rules, scoring, settings model, denylist. **No Outlook or WinForms dependency**; keep it that way so it stays unit-testable. |
| `Lookout.Tests` (net8.0) | MSTest for Core. |
| `Lookout.Office` (net48) | Junk scanner/deleter, ribbon, review pane, settings dialog, logging, settings store. |
| `Lookout.Addin` (VSTO) | Thin `ThisAddIn` shell only. The generated `ThisAddIn.Designer.cs` is from the VS template. |
| `LookoutSetup` (WiX) | MSI. Registers the add-in in both registry views and trusts the manifest signing key. |

## Design notes

- **Outlook's object model is not thread-safe.** The scanner reads the Junk folder in batches of 25 on the UI
  thread from a WinForms `Timer`. Release every COM object (`Marshal.ReleaseComObject`) in a `finally`.
- **Delete is a move to Deleted Items**, not a permanent delete: Outlook.com/M365 blocks permanent delete via
  classic MAPI. Nothing is deleted without the user's click and confirmation. Never auto-delete.
- **Rules.** A rule has a stable key (its description text) used by settings. Sender rules live in
  `SenderRule.Default`, subject rules in `TextRule.Subject`, message-level checks in `Assessor`/`MessageChecks`
  with key constants in `RuleCatalog`. Every produced `Assessment` carries its key so `RuleSettings.Apply` can
  switch it off or re-score it. `RuleCatalog.All` feeds the Settings dialog and a test checks it covers every
  key. The scan-level "same display name from several domains" rule runs in `ReviewPane` after the scan.
- **The ported gibberish rule intentionally does not flag `7IPFEMF6`** (vowel ratio 0.25, rule needs < 0.2); this
  matches the original script and is pinned in a test.
- **User files** are under `%LOCALAPPDATA%\Lookout`: `settings.conf` (only non-default rules are written),
  `denylist.txt`, `lookout.log`. Logging and settings IO must never throw into the UI.
- **Theming.** WinForms controls do not inherit Office colors; `PaneTheme` reads the Office "UI Theme" registry
  value and sets explicit colors and a 10pt font.
- **Public key trust.** VSTO only loads signed manifests from a trusted publisher; the MSI writes the signing
  cert's public key to `VSTO\Security\Inclusion`. The csproj currently uses the VS temporary key.

## Conventions

- **Copyright header** on every `.cs` file, three lines, year 2026, LF line endings, no BOM:
  `//****...` / `// Copyright © 2026 Steven M Cohn. All rights reserved.` / `//****...` (copy from any file).
- Always use curly braces for `if`/`else`, even one-liners. Use `is null` / `is not null`.
- Use `var` when the type is obvious; order methods so callers come before callees.
- **Commits are GPG-signed** (the signing key comes from `~/git-personal.conf`, included only when the repo has a
  `github.com/stevencohn` remote). Do not commit or push until asked.
- License: MPL 2.0.
- Shell: prefer PowerShell.
