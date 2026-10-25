using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ElecKit.Core
{
    /// <summary>
    /// Base class for every tool: one place for logging, error handling and cancel handling.
    /// Derived classes still need their own [Transaction(...)] attribute.
    /// </summary>
    public abstract class ElecCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Log.Info("Run " + GetType().Name);
                return Run(new ToolContext(commandData), ref message);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                message = ex.Message;
                return Result.Failed;
            }
        }

        protected abstract Result Run(ToolContext ctx, ref string message);
    }
}
