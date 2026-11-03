//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Lookout.Core;
using Lookout.Core.Rules;

namespace Lookout.Office
{
	/// <summary>
	/// Task pane that scans Junk Email in batches on the UI thread and lets the
	/// user pick which flagged messages to move to Deleted Items. Nothing moves
	/// until the user clicks Delete.
	/// </summary>
	public sealed class ReviewPane : UserControl
	{
		private const int BatchSize = 25;

		private readonly Microsoft.Office.Interop.Outlook.Application app;
		private readonly Timer timer = new Timer { Interval = 15 };
		private readonly Button scanButton = new Button { Text = "Scan", AutoSize = true };
		private readonly Button selectButton = new Button { Text = "Select suspicious", AutoSize = true };
		private readonly Button moveButton = new Button { Text = "Delete", AutoSize = true, Enabled = false };
		private readonly Button exportButton = new Button { Text = "Export CSV", AutoSize = true, Enabled = false };
		private readonly Button settingsButton = new Button { Text = "Settings...", AutoSize = true };
		private readonly List<JunkRow> scanned = new List<JunkRow>();
		private readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Top, Height = 10 };
		private readonly Label status = new Label { Dock = DockStyle.Top, AutoSize = false, Height = 28, TextAlign = ContentAlignment.MiddleLeft };
		private readonly DataGridView grid = new DataGridView();

		private JunkScanner scanner;
		private RuleSettings activeSettings = new RuleSettings();
		private readonly Dictionary<Verdict, int> counts = new Dictionary<Verdict, int>();

		public ReviewPane(Microsoft.Office.Interop.Outlook.Application app)
		{
			this.app = app;
			AutoScaleMode = AutoScaleMode.None;
			Font = PaneTheme.CreateFont();
			BuildLayout();

			scanButton.Click += (s, e) => StartScan();
			selectButton.Click += (s, e) => SelectSuspicious();
			moveButton.Click += (s, e) => MoveSelected();
			exportButton.Click += (s, e) => ExportCsv();
			settingsButton.Click += (s, e) => OpenSettings();
			timer.Tick += (s, e) => ScanNextBatch();
		}

		private void BuildLayout()
		{
			var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
			buttons.Controls.AddRange(new Control[] { scanButton, selectButton, moveButton, exportButton, settingsButton });

			grid.Dock = DockStyle.Fill;
			grid.AllowUserToAddRows = false;
			grid.AllowUserToDeleteRows = false;
			grid.RowHeadersVisible = false;
			grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
			PaneTheme.FitGridToFont(grid);

			grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Move", HeaderText = "Move" });
			foreach (var name in new[] { "Verdict", "Subject", "Sender", "OnBehalfOf", "Received", "Reasons" })
			{
				grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = name, ReadOnly = true });
			}

			// Fill docking order: last added is docked first
			Controls.Add(grid);
			Controls.Add(status);
			Controls.Add(progress);
			Controls.Add(buttons);

			PaneTheme.Apply(this, grid);
		}

		private void OpenSettings()
		{
			using (var dialog = new SettingsDialog(SettingsStore.Load()))
			{
				if (dialog.ShowDialog(this) != DialogResult.OK)
				{
					return;
				}

				if (!SettingsStore.Save(dialog.Result))
				{
					MessageBox.Show($"Could not save settings to {SettingsStore.SettingsPath}.",
						"Lookout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}
			}

			// verdicts depend on the settings, so rescan with them
			StartScan();
		}

		/// <summary>Starts a scan unless one is already running; called when the pane is shown.</summary>
		public void ScanIfIdle()
		{
			if (!timer.Enabled)
			{
				StartScan();
			}
		}

		private void StartScan()
		{
			timer.Stop();
			scanner?.Dispose();
			grid.Rows.Clear();
			counts.Clear();
			scanned.Clear();
			exportButton.Enabled = false;
			moveButton.Enabled = false;
			scanButton.Enabled = false;

			activeSettings = SettingsStore.Load();
			scanner = new JunkScanner(app, activeSettings, SettingsStore.LoadDenylist());
			progress.Maximum = Math.Max(1, scanner.Total);
			progress.Value = 0;
			status.Text = $"Scanning {scanner.Total} items...";
			timer.Start();
		}

		private void ScanNextBatch()
		{
			foreach (var row in scanner.ReadBatch(BatchSize))
			{
				AddRow(row);
			}

			progress.Value = Math.Min(progress.Maximum, scanner.Scanned);

			if (scanner.Done)
			{
				timer.Stop();
				scanButton.Enabled = true;
				moveButton.Enabled = grid.Rows.Count > 0;
				exportButton.Enabled = scanned.Count > 0;
				ApplyRepeatedSenders();
				status.Text = $"Suspicious: {Count(Verdict.Suspicious)}   Uncertain: {Count(Verdict.Uncertain)}   Looks legit: {Count(Verdict.LooksLegit)}";
				Logger.Info($"Scan complete: {status.Text}");
				grid.Sort(new SeverityComparer());
				if (grid.Rows.Count > 0)
				{
					grid.FirstDisplayedScrollingRowIndex = 0;
				}
				SelectSuspicious();
			}
		}

		/// <summary>
		/// Scan-level rule: a display name that arrives from several unrelated domains gets extra
		/// points on every one of its messages, then the verdicts and the grid are refreshed.
		/// </summary>
		private void ApplyRepeatedSenders()
		{
			var names = RepeatedSenders.FindNames(scanned.Select(r => new KeyValuePair<string, string>(r.SenderName, r.SenderEmail)));
			if (names.Count == 0)
			{
				return;
			}

			var finding = activeSettings.Apply(new Assessment(
				$"[Sender] {RuleCatalog.RepeatedSender}", 2, RuleCatalog.RepeatedSender));
			if (finding is null)
			{
				return;
			}

			foreach (var row in scanned)
			{
				var key = RepeatedSenders.NormalizeName(row.SenderName);
				if (key is not null && names.Contains(key))
				{
					row.Score += finding.Score;
					row.Reasons = string.IsNullOrEmpty(row.Reasons) ? finding.Reason : row.Reasons + "; " + finding.Reason;
					row.Verdict = Assessor.VerdictFor(row.Score, activeSettings);
				}
			}

			counts.Clear();
			foreach (var row in scanned)
			{
				counts[row.Verdict] = Count(row.Verdict) + 1;
			}

			foreach (DataGridViewRow gridRow in grid.Rows)
			{
				var row = (JunkRow)gridRow.Tag;
				gridRow.Cells["Verdict"].Value = row.Verdict.ToString();
				gridRow.Cells["Reasons"].Value = row.Reasons;
			}
		}

		private int Count(Verdict verdict)
		{
			return counts.TryGetValue(verdict, out var n) ? n : 0;
		}

		private void AddRow(JunkRow row)
		{
			counts[row.Verdict] = Count(row.Verdict) + 1;
			scanned.Add(row);

			var sender = $"{row.SenderName} <{row.SenderEmail}>";
			var onBehalfOf = string.IsNullOrEmpty(row.OnBehalfOfName) && string.IsNullOrEmpty(row.OnBehalfOfEmail)
				? string.Empty
				: $"{row.OnBehalfOfName} <{row.OnBehalfOfEmail}>";

			var index = grid.Rows.Add(false, row.Verdict.ToString(), row.Subject, sender, onBehalfOf, row.Received.ToString("g"), row.Reasons);
			grid.Rows[index].Tag = row;
		}

		/// <summary>Exports every row from the last scan, including ones already moved.</summary>
		private void ExportCsv()
		{
			using (var dialog = new SaveFileDialog
			{
				Title = "Export scan results",
				Filter = "CSV files (*.csv)|*.csv",
				FileName = $"lookout-junk-{DateTime.Now:yyyyMMdd-HHmm}.csv"
			})
			{
				if (dialog.ShowDialog(this) != DialogResult.OK)
				{
					return;
				}

				try
				{
					JunkCsv.Write(dialog.FileName, scanned);
					status.Text = $"Exported {scanned.Count} rows to {dialog.FileName}";
				}
				catch (System.IO.IOException exc)
				{
					MessageBox.Show(exc.Message, "Lookout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			}
		}

		private void SelectSuspicious()
		{
			foreach (DataGridViewRow gridRow in grid.Rows)
			{
				gridRow.Cells["Move"].Value = ((JunkRow)gridRow.Tag).Verdict == Verdict.Suspicious;
			}
		}

		private void MoveSelected()
		{
			var chosen = new List<DataGridViewRow>();
			foreach (DataGridViewRow gridRow in grid.Rows)
			{
				if (gridRow.Cells["Move"].Value is true)
				{
					chosen.Add(gridRow);
				}
			}

			if (chosen.Count == 0)
			{
				status.Text = "Nothing selected.";
				return;
			}

			var confirm = MessageBox.Show(
				$"Delete {chosen.Count} message(s)? They will be moved to Deleted Items.",
				"Lookout", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

			if (confirm != DialogResult.OK)
			{
				return;
			}

			var junkRows = new List<JunkRow>();
			foreach (var gridRow in chosen)
			{
				junkRows.Add((JunkRow)gridRow.Tag);
			}

			var moved = JunkMover.MoveToDeletedItems(app, junkRows);
			Logger.Info($"Move requested for {junkRows.Count}, moved {moved}");

			foreach (var gridRow in chosen)
			{
				grid.Rows.Remove(gridRow);
			}

			status.Text = $"Deleted {moved} of {chosen.Count} message(s) (moved to Deleted Items).";
		}

		/// <summary>Most severe first: verdict, then score, then newest.</summary>
		private sealed class SeverityComparer : System.Collections.IComparer
		{
			public int Compare(object x, object y)
			{
				var a = (JunkRow)((DataGridViewRow)x).Tag;
				var b = (JunkRow)((DataGridViewRow)y).Tag;

				var result = b.Verdict.CompareTo(a.Verdict);
				if (result == 0)
				{
					result = b.Score.CompareTo(a.Score);
				}

				return result != 0 ? result : b.Received.CompareTo(a.Received);
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				timer.Dispose();
				scanner?.Dispose();
			}

			base.Dispose(disposing);
		}
	}
}
