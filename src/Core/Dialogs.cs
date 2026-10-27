using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ElecKit.Core
{
    /// <summary>The small windows every tool uses to ask questions and show results. All share one look (see Theme).</summary>
    public static class Dialogs
    {
        /// <summary>Returns the entered text, or null if the user cancelled.</summary>
        public static string Ask(string title, string prompt, string initial)
        {
            var box = new TextBox
            {
                Text = initial ?? "",
                MinWidth = 360,
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 8, 0, 0)
            };
            var body = new StackPanel();
            body.Children.Add(Theme.Prompt(prompt));
            body.Children.Add(box);

            var shell = new DialogShell(title, body);
            shell.AddButton("Cancel", false, false, true, null);
            shell.AddButton("OK", true, true, false, (s, e) => shell.Window.DialogResult = true);
            shell.Window.Loaded += (s, e) => { box.Focus(); box.SelectAll(); };
            return shell.Show() == true ? box.Text : null;
        }

        /// <summary>
        /// Asks for a number, with an inline message and a disabled OK button while the entry is not valid.
        /// Returns null if cancelled.
        /// </summary>
        public static double? AskNumber(string title, string prompt, double initial, bool positiveOnly, string unit)
        {
            var box = new TextBox
            {
                Text = UnitConv.Format(initial),
                Width = 150,
                Padding = new Thickness(6, 4, 6, 4)
            };
            var unitLabel = new TextBlock
            {
                Text = unit ?? "",
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Theme.Muted
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            row.Children.Add(box);
            row.Children.Add(unitLabel);

            var error = new TextBlock
            {
                Foreground = Theme.Danger,
                FontSize = 12,
                MinHeight = 18,
                Margin = new Thickness(0, 6, 0, 0)
            };

            var body = new StackPanel();
            body.Children.Add(Theme.Prompt(prompt));
            body.Children.Add(row);
            body.Children.Add(error);

            var shell = new DialogShell(title, body);
            shell.AddButton("Cancel", false, false, true, null);
            Button ok = shell.AddButton("OK", true, true, false, (s, e) => shell.Window.DialogResult = true);

            double value = initial;
            Action validate = () =>
            {
                double parsed;
                if (!UnitConv.TryParseNumber(box.Text, out parsed))
                {
                    error.Text = "Enter a number.";
                    ok.IsEnabled = false;
                }
                else if (positiveOnly && parsed <= 0)
                {
                    error.Text = "Enter a number greater than zero.";
                    ok.IsEnabled = false;
                }
                else
                {
                    error.Text = "";
                    ok.IsEnabled = true;
                    value = parsed;
                }
            };
            box.TextChanged += (s, e) => validate();
            validate();
            shell.Window.Loaded += (s, e) => { box.Focus(); box.SelectAll(); };

            return shell.Show() == true ? (double?)value : null;
        }

        /// <summary>Returns the chosen index, or -1 if the user cancelled. Up to five options show as radio buttons, more as a list.</summary>
        public static int Choose(string title, string prompt, IList<string> options, int selected)
        {
            int start = (selected >= 0 && selected < options.Count) ? selected : 0;
            var body = new StackPanel();
            body.Children.Add(Theme.Prompt(prompt));

            Func<int> read;
            if (options.Count <= 5)
            {
                string group = "choice" + Guid.NewGuid().ToString("N");
                var radios = new List<RadioButton>();
                var list = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
                for (int i = 0; i < options.Count; i++)
                {
                    var radio = new RadioButton
                    {
                        GroupName = group,
                        IsChecked = i == start,
                        Margin = new Thickness(0, 4, 0, 4),
                        Content = new TextBlock { Text = options[i], TextWrapping = TextWrapping.Wrap, MaxWidth = 430 }
                    };
                    radios.Add(radio);
                    list.Children.Add(radio);
                }
                body.Children.Add(list);
                read = () => radios.FindIndex(r => r.IsChecked == true);
            }
            else
            {
                var combo = new ComboBox
                {
                    MinWidth = 360,
                    Margin = new Thickness(0, 8, 0, 0),
                    Padding = new Thickness(6, 4, 6, 4)
                };
                foreach (string option in options) combo.Items.Add(option);
                combo.SelectedIndex = start;
                body.Children.Add(combo);
                read = () => combo.SelectedIndex;
            }

            var shell = new DialogShell(title, body);
            shell.AddButton("Cancel", false, false, true, null);
            shell.AddButton("OK", true, true, false, (s, e) => shell.Window.DialogResult = true);
            return shell.Show() == true ? read() : -1;
        }

        /// <summary>Opens a standard file picker. Returns the chosen path or null.</summary>
        public static string PickFile(string title, string filter)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Title = title;
            dialog.Filter = filter;
            dialog.CheckFileExists = true;
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        /// <summary>Shows read-only text (a report) in a resizable window with a Copy button.</summary>
        public static void ShowText(string title, string text)
        {
            var box = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                AcceptsReturn = true,
                Padding = new Thickness(8),
                BorderBrush = Theme.Line,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12.5,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var shell = new DialogShell(title, box, true, 860, 560);
            shell.AddButton("Copy", false, false, false, (s, e) =>
            {
                try { Clipboard.SetText(text); } catch { }
            });
            shell.AddButton("Close", true, true, true, null);
            shell.Show();
        }

        /// <summary>
        /// Shows the outcome of a tool: a headline, optional detail lines, and an Open button when a file or folder
        /// was produced. Returns true if the user opened the file or folder from the window.
        /// </summary>
        public static bool Result(string title, string summary, IEnumerable<string> details, string openPath)
        {
            var body = new StackPanel();
            body.Children.Add(new TextBlock
            {
                Text = summary,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.Text,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 460
            });

            if (details != null)
            {
                foreach (string line in details.Where(d => !string.IsNullOrWhiteSpace(d)))
                {
                    body.Children.Add(new TextBlock
                    {
                        Text = line,
                        Foreground = Theme.Muted,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 460,
                        Margin = new Thickness(0, 6, 0, 0)
                    });
                }
            }
            if (!string.IsNullOrEmpty(openPath))
            {
                body.Children.Add(new TextBlock
                {
                    Text = openPath,
                    FontSize = 11.5,
                    Foreground = Theme.Muted,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 460,
                    Margin = new Thickness(0, 10, 0, 0)
                });
            }

            bool opened = false;
            var shell = new DialogShell(title, body);
            if (!string.IsNullOrEmpty(openPath))
            {
                shell.AddButton("Open", false, false, false, (s, e) =>
                {
                    try { Shell.Open(openPath); opened = true; }
                    catch (Exception ex) { Log.Error(ex); }
                });
            }
            shell.AddButton("Close", true, true, true, null);
            shell.Show();
            return opened;
        }
    }
}
