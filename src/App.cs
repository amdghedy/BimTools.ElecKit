using System;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                Log.Info("ElecKit starting");
                RibbonBuilder.Build(application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                TaskDialog.Show("ElecKit", "Startup failed:\n" + ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
