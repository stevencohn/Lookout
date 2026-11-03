//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System;
using System.Windows.Forms;
using Lookout.Core;
using Lookout.Core.Rules;

namespace Lookout.Office
{
	/// <summary>
	/// Edits the suspicious threshold, scan-on-open, and each rule's on/off and score.
	/// Result holds the edited settings when the dialog returns OK.
	/// </summary>
	internal sealed class SettingsDialog : Form
	{
		private readonly NumericUpDown threshold = new NumericUpDown { Minimum = 1, Maximum = 50, Width = 70 };
		private readonly CheckBox scanOnOpen = new CheckBox { Text = "Scan when the pane opens", AutoSize = true };
		private readonly DataGridView grid = new DataGridView();
		private readonly Button resetButton = new Button { Text = "Reset to defaults", AutoSize = true };
		private readonly Button okButton = new Button
		{
			Text = "OK",
			DialogResult = DialogResult.OK,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			MinimumSize = new System.Drawing.Size(100, 36)
		};
		private readonly Button cancelButton = new Button
		{
			Text = "Cancel",
			DialogResult = DialogResult.Cancel,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			MinimumSize = new System.Drawing.Size(100, 36)
		};

		public SettingsDialog(RuleSettings current)
		{
			Text = "Lookout settings";
			AutoScaleMode = AutoScaleMode.None;
			Font = PaneTheme.CreateFont();
			StartPosition = FormStartPosition.CenterParent;
			Size = new System.Drawing.Size(980, 640);
			MinimumSize = new System.Drawing.Size(640, 400);
			MinimizeBox = false;
			MaximizeBox = true;
			ShowInTaskbar = false;
			FormBorderStyle = FormBorderStyle.Sizable;
			AcceptButton = okButton;
			CancelButton = cancelButton;

			BuildLayout();
			Populate(current);

			resetButton.Click += (s, e) => Populate(new RuleSettings());
			okButton.Click += (s, e) => grid.EndEdit();
			FormClosing += OnFormClosing;

			PaneTheme.Apply(this, grid);
		}

		/// <summary>The edited settings; valid after the dialog returns OK.</summary>
		public RuleSettings Result { get; private set; }

		private void BuildLayout()
		{
			var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
			top.Controls.Add(new Label { Text = "Mark as Suspicious at a score of", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 4, 0) });
			top.Controls.Add(threshold);
			top.Controls.Add(new Label { Text = "or more", AutoSize = true, Margin = new Padding(4, 6, 24, 0) });
			top.Controls.Add(scanOnOpen);

			grid.Dock = DockStyle.Fill;
			grid.AllowUserToAddRows = false;
			grid.AllowUserToDeleteRows = false;
			grid.RowHeadersVisible = false;
			grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			PaneTheme.FitGridToFont(grid);
			grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "On", Width = 50 });
			grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Score", HeaderText = "Score", Width = 70 });
			grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Default", HeaderText = "Default", Width = 75, ReadOnly = true });
			grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Group", HeaderText = "Group", Width = 90, ReadOnly = true });
			grid.Columns.Add(new DataGridViewTextBoxColumn
			{
				Name = "Rule",
				HeaderText = "Rule",
				ReadOnly = true,
				AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
			});

			var buttons = new FlowLayoutPanel
			{
				Dock = DockStyle.Bottom,
				AutoSize = true,
				FlowDirection = FlowDirection.RightToLeft,
				Padding = new Padding(8)
			};
			buttons.Controls.AddRange(new Control[] { cancelButton, okButton, resetButton });

			Controls.Add(grid);
			Controls.Add(top);
			Controls.Add(buttons);
		}

		private void Populate(RuleSettings settings)
		{
			threshold.Value = Math.Max(threshold.Minimum, Math.Min(threshold.Maximum, settings.SuspiciousThreshold));
			scanOnOpen.Checked = settings.ScanOnOpen;

			grid.Rows.Clear();
			foreach (var info in RuleCatalog.All)
			{
				settings.Rules.TryGetValue(info.Key, out var rule);
				var score = rule?.Score ?? info.DefaultScore;
				var index = grid.Rows.Add(rule?.Enabled ?? true, score, info.DefaultScore, info.Group, info.Key);
				grid.Rows[index].Tag = info;
			}
		}

		private void OnFormClosing(object sender, FormClosingEventArgs e)
		{
			if (DialogResult != DialogResult.OK)
			{
				return;
			}

			var settings = new RuleSettings
			{
				SuspiciousThreshold = (int)threshold.Value,
				ScanOnOpen = scanOnOpen.Checked
			};

			foreach (DataGridViewRow row in grid.Rows)
			{
				var info = (RuleInfo)row.Tag;
				var enabled = row.Cells["Enabled"].Value is true;

				if (!int.TryParse(Convert.ToString(row.Cells["Score"].Value), out var score) || score < 0 || score > 20)
				{
					MessageBox.Show($"The score for \"{info.Key}\" must be a whole number from 0 to 20.",
						"Lookout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					e.Cancel = true;
					grid.CurrentCell = row.Cells["Score"];
					return;
				}

				if (!enabled || score != info.DefaultScore)
				{
					settings.Rules[info.Key] = new RuleOverride
					{
						Enabled = enabled,
						Score = score == info.DefaultScore ? (int?)null : score
					};
				}
			}

			Result = settings;
		}
	}
}
