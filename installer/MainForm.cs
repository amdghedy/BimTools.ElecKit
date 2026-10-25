using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ElecKit.Installer
{
    internal sealed class MainForm : Form
    {
        private readonly List<int> _years = Setup.PayloadYears();
        private readonly CheckedListBox _versions = new CheckedListBox();
        private readonly TextBox _log = new TextBox();
        private readonly Button _install = new Button();
        private readonly Button _uninstall = new Button();

        public MainForm()
        {
            Text = "ElecKit Setup";
            ClientSize = new Size(460, 440);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            var title = new Label
            {
                Text = "ElecKit for Autodesk Revit",
                Font = new Font(Font.FontFamily, 13f, FontStyle.Bold),
                AutoSize = true,
                Left = 16,
                Top = 14
            };
            var hint = new Label
            {
                Text = "Tick the Revit versions to install for. This installs for your Windows user only and needs no administrator rights. Close Revit first.",
                Left = 16,
                Top = 46,
                Width = 428,
                Height = 48
            };

            _versions.Left = 16;
            _versions.Top = 100;
            _versions.Width = 428;
            _versions.Height = 124;
            _versions.CheckOnClick = true;
            foreach (int year in _years)
            {
                bool found = Setup.IsRevitInstalled(year);
                _versions.Items.Add("Revit " + year + (found ? "   (found on this PC)" : ""), found);
            }
            if (_years.Count == 0) hint.Text = "This setup file contains no Revit versions.";

            _install.Text = "Install";
            _install.SetBounds(16, 234, 100, 30);
            _install.Click += (s, e) => Run(true);

            _uninstall.Text = "Uninstall";
            _uninstall.SetBounds(124, 234, 100, 30);
            _uninstall.Click += (s, e) => Run(false);

            var close = new Button { Text = "Close", Left = 344, Top = 234, Width = 100, Height = 30 };
            close.Click += (s, e) => Close();

            _log.SetBounds(16, 276, 428, 148);
            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;

            _install.Enabled = _uninstall.Enabled = _years.Count > 0;
            Controls.AddRange(new Control[] { title, hint, _versions, _install, _uninstall, close, _log });
        }

        private void Run(bool install)
        {
            int done = 0;
            for (int i = 0; i < _years.Count; i++)
            {
                if (!_versions.GetItemChecked(i)) continue;

                string message;
                bool ok = install ? Setup.Install(_years[i], out message) : Setup.Uninstall(_years[i], out message);
                _log.AppendText((ok ? "OK    " : "FAILED  ") + message + Environment.NewLine);
                if (ok) done++;
            }

            if (done == 0 && _log.Text.Length == 0) _log.AppendText("Tick at least one Revit version first." + Environment.NewLine);
            else if (install && done > 0)
                _log.AppendText(Environment.NewLine + "Start Revit. The ElecKit tab appears in the ribbon. If Revit asks about loading the add-in, choose Always Load." + Environment.NewLine);
        }
    }
}
