//************************************************************************************************
// Copyright © 2026 Steven M Cohn. All rights reserved.
//************************************************************************************************

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Lookout.Office
{
	/// <summary>
	/// Applies the Office UI theme (Colorful, Dark Gray, Black, White, System) to the
	/// WinForms review pane, which does not inherit Office colors on its own.
	/// </summary>
	internal static class PaneTheme
	{
		private static readonly Color DarkBack = Color.FromArgb(32, 32, 32);
		private static readonly Color DarkPanel = Color.FromArgb(45, 45, 45);
		private static readonly Color DarkText = Color.FromArgb(240, 240, 240);
		private static readonly Color DarkLine = Color.FromArgb(70, 70, 70);
		private static readonly Color DarkSelect = Color.FromArgb(52, 86, 140);

		/// <summary>The font used by the pane and the settings dialog.</summary>
		public static Font CreateFont()
		{
			return new Font("Segoe UI", 10f);
		}

		/// <summary>Sizes grid rows and headers to the control's font.</summary>
		public static void FitGridToFont(DataGridView grid)
		{
			grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			grid.RowTemplate.Height = grid.Font.Height + 10;
		}

		public static bool IsDark()
		{
			try
			{
				// Office "UI Theme": 0 Colorful, 3 Dark Gray, 4 Black, 5 White, 6 Use system
				using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\16.0\Common"))
				{
					var theme = key?.GetValue("UI Theme") as int?;
					if (theme == 3 || theme == 4)
					{
						return true;
					}

					if (theme == 6)
					{
						using (var personalize = Registry.CurrentUser.OpenSubKey(
							@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
						{
							return personalize?.GetValue("AppsUseLightTheme") as int? == 0;
						}
					}
				}
			}
			catch (Exception)
			{
				// fall through to light
			}

			return false;
		}

		public static void Apply(Control root, DataGridView grid)
		{
			var dark = IsDark();

			var back = dark ? DarkBack : SystemColors.Window;
			var panel = dark ? DarkPanel : SystemColors.Control;
			var text = dark ? DarkText : SystemColors.WindowText;
			var line = dark ? DarkLine : SystemColors.ControlDark;
			var select = dark ? DarkSelect : SystemColors.Highlight;
			var selectText = dark ? DarkText : SystemColors.HighlightText;

			root.BackColor = back;
			root.ForeColor = text;
			ApplyToChildren(root, back, text, panel, line);

			grid.BackgroundColor = back;
			grid.GridColor = line;
			grid.BorderStyle = BorderStyle.None;
			grid.EnableHeadersVisualStyles = false;

			grid.DefaultCellStyle.BackColor = back;
			grid.DefaultCellStyle.ForeColor = text;
			grid.DefaultCellStyle.SelectionBackColor = select;
			grid.DefaultCellStyle.SelectionForeColor = selectText;

			grid.ColumnHeadersDefaultCellStyle.BackColor = panel;
			grid.ColumnHeadersDefaultCellStyle.ForeColor = text;
			grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = panel;
			grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = text;

			if (dark)
			{
				DarkenScrollBars(grid);
				if (root is Form form)
				{
					DarkenTitleBar(form);
				}
			}
		}

		[DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
		private static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

		[DllImport("dwmapi.dll")]
		private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

		/// <summary>
		/// Gives the grid and its scroll bars the dark "Explorer" visual style. The scroll bars are
		/// child controls of the grid whose handles may not exist yet, so apply as each is created.
		/// </summary>
		private static void DarkenScrollBars(DataGridView grid)
		{
			void apply(Control control)
			{
				if (control.IsHandleCreated)
				{
					SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
				}
			}

			void hook(Control control)
			{
				control.HandleCreated += (s, e) => apply(control);
				apply(control);
			}

			hook(grid);
			foreach (Control child in grid.Controls)
			{
				if (child is ScrollBar)
				{
					hook(child);
				}
			}
		}

		/// <summary>Switches the dialog's title bar to dark (Windows 10 20H1 and later).</summary>
		private static void DarkenTitleBar(Form form)
		{
			void apply()
			{
				var on = 1;
				// attribute 20 on current Windows, 19 on early Windows 10 builds
				if (DwmSetWindowAttribute(form.Handle, 20, ref on, sizeof(int)) != 0)
				{
					DwmSetWindowAttribute(form.Handle, 19, ref on, sizeof(int));
				}
			}

			form.HandleCreated += (s, e) => apply();
			if (form.IsHandleCreated)
			{
				apply();
			}
		}

		private static void ApplyToChildren(Control parent, Color back, Color text, Color panel, Color line)
		{
			foreach (Control child in parent.Controls)
			{
				if (child is DataGridView)
				{
					continue;
				}

				child.BackColor = back;
				child.ForeColor = text;

				if (child is Button button)
				{
					button.FlatStyle = FlatStyle.Flat;
					button.BackColor = panel;
					button.FlatAppearance.BorderColor = line;
				}

				ApplyToChildren(child, back, text, panel, line);
			}
		}
	}
}
