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
                Log.Info("COMMAND START name=" + GetType().FullName);
                Result result = Run(new ToolContext(commandData), ref message);
                Log.Info("COMMAND END name=" + GetType().FullName + " result=" + result +
                    (string.IsNullOrWhiteSpace(message) ? "" : " message=" + message));
                return result;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException ex)
            {
                Log.Info("COMMAND CANCELLED name=" + GetType().FullName + " reason=" + ex.Message);
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                Log.Error(new InvalidOperationException("COMMAND FAILED name=" + GetType().FullName, ex));
                message = ex.Message;
                return Result.Failed;
            }
        }

        protected abstract Result Run(ToolContext ctx, ref string message);
    }
}
