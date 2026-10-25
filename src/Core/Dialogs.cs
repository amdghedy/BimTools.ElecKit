using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace ElecKit.Core
{
    /// <summary>Tiny input dialogs, built in code so no XAML files are needed.</summary>
    public static class Dialogs
    {
        /// <summary>Returns the entered text, or null if the user cancelled.</summary>
        public static string Ask(string title, string prompt, string initial)
        {
            var box = new TextBox { Text = initial ?? "", MinWidth = 280, Margin = new Thickness(0, 6, 0, 0) };
            return Show(title, prompt, box, box) ? box.Text : null;
        }

        /// <summary>Returns the chosen index, or -1 if the user cancelled.</summary>
        public static int Choose(string title, string prompt, IList<string> options, int selected)
        {
            var combo = new ComboBox { MinWidth = 280, Margin = new Thickness(0, 6, 0, 0) };
            foreach (string option in options) combo.Items.Add(option);
            combo.SelectedIndex = (selected >= 0 && selected < options.Count) ? selected : 0;
            return Show(title, prompt, combo, combo) ? combo.SelectedIndex : -1;
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

        /// <summary>Shows read-only text (a report) in a scrollable window with a Copy button.</summary>
        public static void ShowText(string title, string text)
        {
            var box = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var window = new Window
            {
                Title = title,
                Width = 780,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar = false,
                Topmost = true
            };

            var copy = new Button { Content = "Copy", Width = 80, Margin = new Thickness(0, 0, 8, 0) };
            var close = new Button { Content = "Close", Width = 80, IsDefault = true, IsCancel = true };
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            buttons.Children.Add(copy);
            buttons.Children.Add(close);

            var layout = new DockPanel { Margin = new Thickness(12) };
            DockPanel.SetDock(buttons, Dock.Bottom);
            layout.Children.Add(buttons);
            layout.Children.Add(box);
            window.Content = layout;

            copy.Click += (s, e) =>
            {
                try { Clipboard.SetText(text); } catch { }
            };
            close.Click += (s, e) => window.Close();

            new WindowInteropHelper(window).Owner = Process.GetCurrentProcess().MainWindowHandle;
            window.ShowDialog();
        }

        private static bool Show(string title, string prompt, Control input, Control focus)
        {
            var window = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar = false,
                Topmost = true
            };

            var panel = new StackPanel { Margin = new Thickness(14) };
            panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap, MaxWidth = 380 });
            panel.Children.Add(input);

            var ok = new Button { Content = "OK", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            window.Content = panel;

            ok.Click += (s, e) => { window.DialogResult = true; };
            window.Loaded += (s, e) =>
            {
                focus.Focus();
                var text = focus as TextBox;
                if (text != null) text.SelectAll();
            };

            new WindowInteropHelper(window).Owner = Process.GetCurrentProcess().MainWindowHandle;
            return window.ShowDialog() == true;
        }
    }
}
