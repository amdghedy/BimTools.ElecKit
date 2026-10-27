using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ElecKit.Core
{
    /// <summary>Editable table for the cable catalog.</summary>
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
                BorderBrush = Theme.Line,
                MinHeight = 240
            };
            grid.AutoGeneratingColumn += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case "CrossSectionMm2": e.Column.Header = "Cross-section (mm\u00B2)"; break;
                    case "OuterDiameterMm": e.Column.Header = "Outer diameter (mm)"; break;
                    case "AmpacityA": e.Column.Header = "Current capacity (A, optional)"; break;
                }
            };

            var info = Theme.Prompt("Edit the values in the table. Use the empty last row to add a cable. Select a row and press Delete to remove it. " +
                                    "The Voltage Drop tool reads the cross-section (and the capacity, when you fill it in).");
            info.MaxWidth = double.PositiveInfinity;
            info.Margin = new Thickness(0, 0, 0, 10);

            var layout = new DockPanel();
            DockPanel.SetDock(info, Dock.Top);
            layout.Children.Add(info);
            layout.Children.Add(grid);

            var shell = new DialogShell("Cable Catalog", layout, true, 880, 520);
            shell.AddButton("Reset to defaults", false, false, false, (s, e) =>
            {
                rows.Clear();
                foreach (CableDefinition d in CableCatalogStore.Defaults()) rows.Add(d);
            });
            shell.AddButton("Cancel", false, false, true, null);
            shell.AddButton("Save", true, true, false, (s, e) =>
            {
                grid.CommitEdit(DataGridEditingUnit.Row, true);
                shell.Window.DialogResult = true;
            });

            if (shell.Show() != true) return null;
            return rows.Where(r => !string.IsNullOrWhiteSpace(r.Name)).ToList();
        }
    }
}
