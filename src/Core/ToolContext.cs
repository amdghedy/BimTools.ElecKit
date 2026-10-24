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

        public ToolContext(ExternalCommandData data)
        {
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
                Log.Info("TRANSACTION START name=" + name + " document=" + Doc.Title);
                try
                {
                    if (!action())
                    {
                        t.RollBack();
                        Log.Info("TRANSACTION ROLLBACK name=" + name + " reason=action-returned-false status=" + t.GetStatus());
                        return false;
                    }
                    TransactionStatus committed = t.Commit();
                    Log.Info("TRANSACTION COMMIT name=" + name + " status=" + committed);
                    if (committed != TransactionStatus.Committed)
                        throw new InvalidOperationException("Transaction '" + name + "' did not commit. Status: " + committed);
                    return true;
                }
                catch (Exception ex)
                {
                    try
                    {
                        if (t.GetStatus() == TransactionStatus.Started)
                        {
                            t.RollBack();
                            Log.Info("TRANSACTION ROLLBACK name=" + name + " reason=exception status=" + t.GetStatus());
                        }
                    }
                    catch (Exception rollbackError)
                    {
                        Log.Error(new InvalidOperationException("Rollback failed for transaction '" + name + "'.", rollbackError));
                    }
                    Log.Error(new InvalidOperationException("TRANSACTION FAILED name=" + name, ex));
                    throw;
                }
            }
        }
    }
}
