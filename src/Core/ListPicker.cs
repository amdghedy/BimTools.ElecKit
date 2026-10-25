using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace ElecKit.Core
{
    /// <summary>A list with a search box. Double-click or OK picks an item.</summary>
    public static class ListPicker
    {
        /// <summary>Returns the index in <paramref name="items"/> of the chosen entry, or -1 if cancelled.</summary>
        public static int Pick(string title, string prompt, IList<string> items)
        {
            var filterBox = new TextBox { Margin = new Thickness(0, 6, 0, 6) };
            var list = new ListBox { MinHeight = 260, MaxHeight = 420, MinWidth = 440 };
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
            };

            var window = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar = false,
                Topmost = true
            };

            var ok = new Button { Content = "OK", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var panel = new StackPanel { Margin = new Thickness(14) };
            panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap, MaxWidth = 440 });
            panel.Children.Add(filterBox);
            panel.Children.Add(list);
            panel.Children.Add(buttons);
            window.Content = panel;

            int result = -1;
            Action accept = () =>
            {
                if (list.SelectedIndex < 0) return;
                result = shown[list.SelectedIndex];
                window.DialogResult = true;
            };

            filterBox.TextChanged += (s, e) => refresh();
            ok.Click += (s, e) => accept();
            list.MouseDoubleClick += (s, e) => accept();
            window.Loaded += (s, e) => filterBox.Focus();

            refresh();
            new WindowInteropHelper(window).Owner = Process.GetCurrentProcess().MainWindowHandle;
            return window.ShowDialog() == true ? result : -1;
        }
    }
}
