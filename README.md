# Lookout

Lookout is an add-in for classic Outlook that reviews your **Junk Email** folder. It scores each message's
sender identity, subject and mail headers against a set of red flags, ranks the messages from most to least
suspicious, and lets you delete the ones you choose with one click.

Junk folders fill up with spam that slips past filters by dressing up the sender: random gibberish domains,
invisible Unicode characters inside brand names, look-alike letters, or forged "on behalf of" identities.
Lookout looks for exactly those tricks, so you can clear the obvious junk in seconds and spot the odd one out.

Everything runs locally inside Outlook. Lookout makes no network calls and sends nothing anywhere.

## Requirements

- **Outlook 2013 or later, classic desktop edition** (Outlook 2013, 2016, 2019, 2021, and Microsoft 365 Apps).
  32-bit and 64-bit Outlook are both supported.
- Windows 10 or 11.
- .NET Framework 4.8.
- The Visual Studio 2010 Tools for Office Runtime. It is installed with Microsoft 365 Apps and recent Office
  versions; the installer checks for it and tells you where to get it if it is missing.

**Not supported:** the new Outlook for Windows and Outlook on the web. They do not load VSTO or COM add-ins.

## Installing

Run `Lookout_<version>_Setup.msi` (administrator rights are required) and follow the wizard. The add-in is
installed to `C:\Program Files\River\Lookout` for all users. Restart Outlook; a **Lookout** group appears on the
Mail ribbon.

To uninstall, remove **Lookout** in Windows Settings > Apps.

## Using Lookout

1. In Outlook's Mail view, choose **Lookout > Review Junk** on the ribbon. A pane opens on the right and scans
   the Junk Email folder.
2. When the scan finishes, the list is sorted most severe first. Messages judged **Suspicious** are already
   checked. Each row shows the verdict, subject, sender, any "on behalf of" identity, the received time, and
   the reasons it was flagged.
3. Review the list, tick or untick rows, then click **Delete**. After you confirm, the checked messages move to
   the Deleted Items folder.

The pane's buttons:

| Button | What it does |
|--------|--------------|
| **Scan** | Rescans the Junk folder. The pane also scans automatically when it opens (configurable). |
| **Select suspicious** | Checks every Suspicious row and unchecks the rest. |
| **Delete** | Moves the checked messages to Deleted Items, after a confirmation. |
| **Export CSV** | Saves every row from the last scan to a CSV file, in the same most-severe-first order. |
| **Settings...** | Opens the settings dialog (see below). |

Nothing is ever deleted without your click. **Delete** moves messages to Deleted Items; it does not remove them
permanently. Outlook.com and Microsoft 365 mailboxes do not allow permanent deletion through Outlook's classic
object model, so your mailbox's normal retention applies from there.

Lookout follows the Office theme (Colorful, Dark Gray, Black, White, or "Use system"). The theme is read when the
pane is first created, so reopen the pane after changing it.

## How messages are scored

Each rule that matches adds points to a message's score:

| Score | Verdict |
|-------|---------|
| 0 | **Looks legit** |
| 1 up to the threshold | **Uncertain** |
| the threshold or more (default 3) | **Suspicious** |

Rules marked *Sender* are applied to the actual sender and, when a message has one, to the separate "on behalf
of" identity (Outlook's `Sender <a@x.com>; on behalf of; Other <b@y.com>` line). Reasons are shown with a
`[Sender]` or `[On behalf of]` prefix so you can see which identity triggered them.

### Sender rules

| Rule | Points | What it catches |
|------|--------|-----------------|
| Domain label starts or ends with a hyphen | 4 | Invalid DNS syntax, e.g. `info@-----mail.xyz.com` or `x@---Service-abc.com`. Real registrars never allow it, so the domain is machine-generated. |
| Domain has no dot | 4 | e.g. `7IPFEMF6@FNGNOHNG2AL05U46FM9FJTUM29PW`: it can never resolve on the internet. |
| Display name has hidden Unicode characters | 3 | Zero-width characters and invisible Unicode "tag" characters stitched into a brand name to defeat spam filters while still rendering normally. |
| Domain has a random-looking label | 3 | Machine-generated hostnames such as `7ahYJKxf2u` (mixed case plus digits) or a long all-caps run with several digits. Real domains are lower case. |
| Display name mixes Latin with Greek/Cyrillic | 3 | A look-alike letter from another alphabet inside a word, e.g. a Cyrillic "а" in "Paypal". |
| Domain has an all-numeric label | 2 | e.g. `foo.32652322.com`: not how registered domains look. |
| Display name looks like gibberish | 2 | A single unbroken token of random capital letters and digits, e.g. `1ASDFVAS83SF8`. |
| Email local part looks like gibberish | 2 | The part before the `@` is random characters. |
| Display name has a look-alike character | 2 | A capital `I`, `0` or `1` standing in for a letter inside a word, e.g. "FormuIa" or "Micr0soft". Names such as McIntosh are excluded. |
| Sender and "on behalf of" domains differ | 2 | The submitting server and the claimed identity are unrelated domains. Common with legitimate bulk mailers, so it only matters combined with other flags. |

### Subject rules

| Rule | Points | What it catches |
|------|--------|-----------------|
| Subject contains your own mailbox name | 3 | Bulk-mail tools personalize subjects with your account name, e.g. "stevencohn Apply Today!". Names shorter than 5 characters are ignored. |
| Subject mixes Latin with Greek/Cyrillic | 3 | Same look-alike trick, in the subject. |
| Subject has a look-alike character | 2 | Same `I`/`0`/`1` substitution, in the subject. |

### Header rules

Read from the message's `Authentication-Results` header, which your mail provider adds when it checks the sender.

| Rule | Points | What it catches |
|------|--------|-----------------|
| DMARC check failed | 3 | The message does not align with the From domain's published policy. |
| SPF check failed | 2 | The sending server is not authorized to send for the domain. |
| SPF soft-failed | 1 | The server is probably not authorized. |
| DKIM signature failed | 1 | Ignored if another DKIM signature on the message passed. |

These are heuristics. Legitimate mail occasionally trips a rule (for example a mailing list that breaks DKIM), and
spam occasionally trips none, so always glance at the list before you click Delete. Rules you disagree with can be
switched off or re-scored in Settings.

## Settings

**Settings...** in the pane opens a resizable dialog where you can change:

- the score at which a message counts as **Suspicious** (default 3),
- whether to scan automatically when the pane opens,
- the on/off switch and score of every rule above.

**Reset to defaults** restores everything. Saving rescans with the new values.

Settings are stored in `%LOCALAPPDATA%\Lookout\settings.conf`, a small text file you can also edit by hand. Only
rules that differ from their defaults are written:

```
SuspiciousThreshold=3
ScanOnOpen=true
Rule=0|-|Domain has an all-numeric label (not a registered-looking domain)
Rule=1|5|SPF check failed (sending server not authorized)
```

A `Rule` line is `<1 or 0 for on/off>|<score, or - for the default>|<the rule's text>`.

## Logs

Scans and deletions are logged to `%LOCALAPPDATA%\Lookout\lookout.log` (rolled to `lookout.old.log` at 1 MB). Each
deleted message is recorded with its subject, sender, verdict and reasons, so you can find it in Deleted Items
later.

## Troubleshooting

- **No Lookout group on the ribbon:** check File > Options > Add-ins. If Lookout is listed under *Disabled Items*
  or *Inactive*, enable it. Make sure you are using classic Outlook, and that the VSTO runtime is installed.
- **A message was flagged wrongly:** open Settings and lower the score of, or switch off, the rule named in its
  reasons.
- **Nothing was found:** the add-in scans the default Junk Email folder of your default mail store.

## Building from source

Requirements: Visual Studio 2022 or later with the *Office/SharePoint development* workload, and the .NET 8 SDK
for the tests.

```powershell
.\build.ps1                  # tests, Release build of the add-in, then the MSI
.\build.ps1 -Version 1.1.0   # set the MSI version
.\build.ps1 -SkipTests
```

`build.ps1` is the supported way to build. The VSTO project needs Visual Studio's MSBuild, not `dotnet build`,
and the installer needs the manifest-signing key that `build.ps1` extracts. The MSI lands in
`LookoutSetup\bin\Release\Lookout_<version>_Setup.msi`.

To run tests alone: `dotnet test Lookout.Tests`.

### Running from Visual Studio

1. Open `Lookout.sln`. Visual Studio needs the *Office/SharePoint development* workload.
2. Set **`Lookout.Addin`** as the startup project (right-click it > *Set as Startup Project*). It is the only
   VSTO project; the others are libraries and cannot be run.
3. **Close Outlook completely** (check Task Manager for a leftover `OUTLOOK.EXE`). If Outlook is already
   running, Visual Studio cannot attach and the new build is not loaded.
4. Press **F5**. Visual Studio builds, registers the add-in, and starts Outlook with the debugger attached, so
   breakpoints in any project work. If Outlook does not start, open the `Lookout.Addin` project properties >
   Debug, and set *Start external program* to `OUTLOOK.EXE`.
5. In Outlook, choose **Lookout > Review Junk** on the Mail ribbon.

Notes:

- Each build registers the add-in under `HKCU\Software\Microsoft\Office\Outlook\Addins\Lookout.Addin`, pointing at
  `Lookout.Addin\bin\<Configuration>`. After that, Outlook loads that build every time it starts, with or without
  Visual Studio, so you can also just rebuild, open Outlook, and test.
- Close Outlook before every rebuild, or the build fails on locked DLLs.
- That per-user registration takes precedence over an installed copy. To test the installed MSI on the same
  machine, delete the `HKCU` key above first.
- Check `%LOCALAPPDATA%\Lookout\lookout.log` to see what the add-in did.
- If Outlook disables the add-in after a crash, re-enable it under File > Options > Add-ins > Manage: Disabled
  Items.

### Projects

| Project | Target | Role |
|---------|--------|------|
| `Lookout.Core` | netstandard2.0 | Rules, scoring, settings model and parsing. No Outlook dependency. |
| `Lookout.Tests` | net8.0 | MSTest unit tests for Core. |
| `Lookout.Office` | net48 | Junk scanner and deleter, ribbon, review pane, settings dialog (uses the Outlook PIA). |
| `Lookout.Addin` | net48 VSTO | The thin add-in shell (`ThisAddIn`). |
| `LookoutSetup` | WiX 7 | MSI installer. |

### The installer

The MSI installs the add-in files to `C:\Program Files\River\Lookout`, registers it machine-wide for both 32- and
64-bit Outlook (it writes both registry views), and adds the manifest-signing certificate's public key to the VSTO
trust list (`Security\Inclusion`). The VSTO manifests are signed with the certificate named in
`Lookout.Addin.csproj`, currently the Visual Studio temporary key. That is fine for your own machines because the
installer trusts it explicitly; to distribute more widely, sign with a real code-signing certificate (Project
Properties > Signing).

### Adding a rule

1. Add the check to `IdentityChecks` (or `MessageChecks`) in `Lookout.Core\Rules`.
2. Add an entry to `SenderRule.Default` (sender rules) or `TextRule.Subject` (subject rules) with a description
   and default score. It then appears in the Settings dialog automatically. Message-level checks (headers,
   cross-check, mailbox name) also need a key constant in `RuleCatalog`.
3. Add a test in `Lookout.Tests`.

## License

[Mozilla Public License 2.0](LICENSE).
