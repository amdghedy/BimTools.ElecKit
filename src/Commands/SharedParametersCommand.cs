using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Creates a small set of common electrical instance parameters in a shared parameter file
    /// (yours if one is set, otherwise one in %APPDATA%\ElecKit) and binds them to the electrical categories.
    /// Parameters that already exist in the project are left alone.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SharedParametersCommand : ElecCommand
    {
        private const string GroupName = "ElecKit";

        private static readonly string[] Names =
        {
            "ElecKit Circuit Text", "ElecKit Switch Label", "ElecKit Panel Text",
            "ElecKit Cable Size", "ElecKit Notes", "ElecKit Mounting Height"
        };

        private static readonly ParameterType[] Types =
        {
            ParameterType.Text, ParameterType.Text, ParameterType.Text,
            ParameterType.Text, ParameterType.Text, ParameterType.Length
        };

        private static readonly BuiltInCategory[] Categories =
        {
            BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_ElectricalEquipment,
            BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_LightingDevices,
            BuiltInCategory.OST_DataDevices, BuiltInCategory.OST_Conduit, BuiltInCategory.OST_CableTray
        };

        protected override Result Run(ToolContext ctx, ref string message)
        {
            var app = ctx.UiApp.Application;

            string path = app.SharedParametersFilename;
            bool usedOwnFile = false;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ElecKit", "ElecKit_SharedParameters.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (!File.Exists(path)) using (File.Create(path)) { }
                app.SharedParametersFilename = path;
                usedOwnFile = true;
            }

            DefinitionFile file = app.OpenSharedParameterFile();
            if (file == null)
            {
                TaskDialog.Show("Shared Parameters", "Revit could not open the shared parameter file:\n" + path);
                return Result.Failed;
            }

            var categories = app.Create.NewCategorySet();
            foreach (BuiltInCategory id in Categories)
            {
                Category category = ctx.Doc.Settings.Categories.get_Item(id);
                if (category != null && category.AllowsBoundParameters) categories.Insert(category);
            }

            var bound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DefinitionBindingMapIterator iterator = ctx.Doc.ParameterBindings.ForwardIterator();
            while (iterator.MoveNext()) bound.Add(iterator.Key.Name);

            int created = 0, existing = 0, failed = 0;
            ctx.Transact("Shared parameters", () =>
            {
                DefinitionGroup group = file.Groups.get_Item(GroupName) ?? file.Groups.Create(GroupName);

                for (int i = 0; i < Names.Length; i++)
                {
                    if (bound.Contains(Names[i])) { existing++; continue; }

                    Definition definition = group.Definitions.get_Item(Names[i]);
                    if (definition == null)
                        definition = group.Definitions.Create(new ExternalDefinitionCreationOptions(Names[i], Types[i]));

                    InstanceBinding binding = app.Create.NewInstanceBinding(categories);
                    if (ctx.Doc.ParameterBindings.Insert(definition, binding, BuiltInParameterGroup.PG_IDENTITY_DATA)) created++;
                    else failed++;
                }
            });

            string text = created + " parameter(s) created and bound, " + existing + " already in the project.";
            if (failed > 0) text += "\n" + failed + " could not be bound.";
            text += "\n\nShared parameter file:\n" + path;
            if (usedOwnFile) text += "\n(No shared parameter file was set, so this one is now active in Revit.)";
            TaskDialog.Show("Shared Parameters", text);
            return Result.Succeeded;
        }
    }
}
