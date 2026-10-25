using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ElecKit.Core
{
    public sealed class ToolContext
    {
        public UIApplication UiApp { get; private set; }
        public UIDocument UiDoc { get; private set; }
        public Document Doc { get; private set; }
        public ExternalCommandData Data { get; private set; }

        public ToolContext(ExternalCommandData data)
        {
            Data = data;
            UiApp = data.Application;
            UiDoc = UiApp.ActiveUIDocument;
            Doc = UiDoc.Document;
        }

        /// <summary>Runs the action in one transaction; rolls back and rethrows on any error.</summary>
        public void Transact(string name, Action action)
        {
            TryTransact(name, delegate { action(); return true; });
        }

        /// <summary>Commits only if the function returns true; otherwise rolls back. Returns the function's result.</summary>
        public bool TryTransact(string name, Func<bool> action)
        {
            using (var t = new Transaction(Doc, "ElecKit: " + name))
            {
                t.Start();
                try
                {
                    if (action())
                    {
                        t.Commit();
                        return true;
                    }
                    t.RollBack();
                    return false;
                }
                catch
                {
                    if (t.GetStatus() == TransactionStatus.Started) t.RollBack();
                    throw;
                }
            }
        }
    }
}
