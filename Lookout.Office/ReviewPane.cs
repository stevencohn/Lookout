//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Lookout.Core;

namespace Lookout.Office
{
	/// <summary>
	/// Task pane that scans Junk Email in batches on the UI thread and lets the
	/// user pick which flagged messages to move to Deleted Items. Nothing moves
	/// until the user clicks the move button.
	/// </summary>
	public sealed class ReviewPane : UserControl
	{
		private const int BatchSize = 25;

		private readonly Microsoft.Office.Interop.Outlook.Application app;
		private readonly Timer timer = new Timer { Interval = 15 };
		private readonly Button scanButton = new Button { Text = "Scan", AutoSize = true };
		private readonly Button selectButton = new Button { Text = "Select suspicious", AutoSize = true };
		private readonly Button moveButton = new Button { Text = "Move selected to Deleted Items", AutoSize = true, Enabled = false };
		private readonly Button exportButton = new Button { Text = "Export CSV", AutoSize = true, Enabled = false };
		private readonly List<JunkRow> scanned = new List<JunkRow>();
		private readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Top, Height = 8 };
		private readonly Label status = new Label { Dock = DockStyle.Top, AutoSize = false, Height = 22, TextAlign = ContentAlignment.MiddleLeft };
		private readonly DataGridView grid = new DataGridView();

		private JunkScanner scanner;
		private readonly Dictionary<Verdict, int> counts = new Dictionary<Verdict, int>();

		public ReviewPane(Microsoft.Office.Interop.Outlook.Application app)
		{
			this.app = app;
			BuildLayout();

			scanButton.Click += (s, e) => StartScan();
			selectButton.Click += (s, e) => SelectSuspicious();
			moveButton.Click += (s, e) => MoveSelected();
			exportButton.Click += (s, e) => ExportCsv();
			timer.Tick += (s, e) => ScanNextBatch();
		}

		private void BuildLayout()
		{
			var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
			buttons.Controls.AddRange(new Control[] { scanButton, selectButton, moveButton, exportButton });

			grid.Dock = DockStyle.Fill;
			grid.AllowUserToAddRows = false;
			grid.AllowUserToDeleteRows = false;
			grid.RowHeadersVisible = false;
			grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;

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

			scanner = new JunkScanner(app);
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
				status.Text = $"Suspicious: {Count(Verdict.Suspicious)}   Uncertain: {Count(Verdict.Uncertain)}   Looks legit: {Count(Verdict.LooksLegit)}";
				Logger.Info($"Scan complete: {status.Text}");
				SelectSuspicious();
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
				$"Move {chosen.Count} message(s) to Deleted Items?",
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

			status.Text = $"Moved {moved} of {chosen.Count} message(s) to Deleted Items.";
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
