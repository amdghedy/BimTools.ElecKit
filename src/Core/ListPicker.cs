using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ElecKit.Core
{
    /// <summary>A list with a search box. Double-click or OK picks an item.</summary>
    public static class ListPicker
    {
        /// <summary>Returns the index in <paramref name="items"/> of the chosen entry, or -1 if cancelled.</summary>
        public static int Pick(string title, string prompt, IList<string> items)
        {
            var filterBox = new TextBox { Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 8, 0, 0) };
            var list = new ListBox { MinHeight = 260, MaxHeight = 380, MinWidth = 520, Margin = new Thickness(0, 8, 0, 0), BorderBrush = Theme.Line };
            var count = new TextBlock { Foreground = Theme.Muted, FontSize = 11.5, Margin = new Thickness(0, 6, 0, 0) };
            var shown = new List<int>();

            Action refresh = () =>
            {
                string needle = filterBox.Text.Trim();
                list.Items.Clear();
                shown.Clear();
                for (int i = 0; i < items.Count; i++)
                {
                    if (needle.Length == 0 || items[i].IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        shown.Add(i);
                        list.Items.Add(items[i]);
                    }
                }
                if (list.Items.Count > 0) list.SelectedIndex = 0;
                count.Text = shown.Count + " of " + items.Count + (needle.Length > 0 ? " match \"" + needle + "\"" : "");
            };

            var body = new StackPanel();
            body.Children.Add(Theme.Prompt(prompt));
            body.Children.Add(filterBox);
            body.Children.Add(list);
            body.Children.Add(count);

            var shell = new DialogShell(title, body);
            int result = -1;
            Action accept = () =>
            {
                if (list.SelectedIndex < 0) return;
                result = shown[list.SelectedIndex];
                shell.Window.DialogResult = true;
            };

            shell.AddButton("Cancel", false, false, true, null);
            shell.AddButton("OK", true, true, false, (s, e) => accept());
            filterBox.TextChanged += (s, e) => refresh();
            list.MouseDoubleClick += (s, e) => accept();
            shell.Window.Loaded += (s, e) => filterBox.Focus();

            refresh();
            return shell.Show() == true ? result : -1;
        }
    }
}
