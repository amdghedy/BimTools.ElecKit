using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace ElecKit.Core
{
    /// <summary>Editable table for the cable catalog, built in code.</summary>
    public static class CableCatalogWindow
    {
        /// <summary>Returns the edited rows, or null if the user cancelled.</summary>
        public static List<CableDefinition> Edit(List<CableDefinition> items)
        {
            var rows = new ObservableCollection<CableDefinition>(items);

            var grid = new DataGrid
            {
                ItemsSource = rows,
                AutoGenerateColumns = true,
                CanUserAddRows = true,
                CanUserDeleteRows = true,
                MinWidth = 640,
                MinHeight = 300
            };
            grid.AutoGeneratingColumn += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case "CrossSectionMm2": e.Column.Header = "Cross-section (mm\u00B2)"; break;
                    case "OuterDiameterMm": e.Column.Header = "Outer diameter (mm)"; break;
                }
            };

            var window = new Window
            {
                Title = "Cable Catalog",
                Width = 780,
                Height = 460,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar = false,
                Topmost = true
            };

            var info = new TextBlock
            {
                Text = "Edit the values in the table. Use the empty last row to add a cable. Select a row and press Delete to remove it.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var reset = new Button { Content = "Reset to defaults", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 16, 0) };
            var ok = new Button { Content = "Save", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            buttons.Children.Add(reset);
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var layout = new DockPanel { Margin = new Thickness(14) };
            DockPanel.SetDock(info, Dock.Top);
            DockPanel.SetDock(buttons, Dock.Bottom);
            layout.Children.Add(info);
            layout.Children.Add(buttons);
            layout.Children.Add(grid);
            window.Content = layout;

            reset.Click += (s, e) =>
            {
                rows.Clear();
                foreach (CableDefinition d in CableCatalogStore.Defaults()) rows.Add(d);
            };
            ok.Click += (s, e) =>
            {
                grid.CommitEdit(DataGridEditingUnit.Row, true);
                window.DialogResult = true;
            };

            new WindowInteropHelper(window).Owner = Process.GetCurrentProcess().MainWindowHandle;
            if (window.ShowDialog() != true) return null;

            return rows.Where(r => !string.IsNullOrWhiteSpace(r.Name)).ToList();
        }
    }
}
