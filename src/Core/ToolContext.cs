using System;
using System.Diagnostics;
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
            using (var transaction = new Transaction(Doc, "ElecKit: " + name))
            {
                var stopwatch = Stopwatch.StartNew();
                Log.Info("TRANSACTION REQUEST name=" + name + " document=" + Doc.Title);
                try
                {
                    TransactionStatus startStatus = transaction.Start();
                    Log.Info("TRANSACTION START name=" + name + " status=" + startStatus + " document=" + Doc.Title);
                    if (startStatus != TransactionStatus.Started)
                        throw new InvalidOperationException("Transaction '" + name + "' failed to start. Status: " + startStatus);

                    if (!action())
                    {
                        TransactionStatus rollbackStatus = transaction.RollBack();
                        Log.Info("TRANSACTION ROLLBACK name=" + name + " reason=action-returned-false status=" + rollbackStatus);
                        if (rollbackStatus != TransactionStatus.RolledBack)
                            throw new InvalidOperationException("Transaction '" + name + "' did not roll back cleanly. Status: " + rollbackStatus);
                        return false;
                    }

                    TransactionStatus commitStatus = transaction.Commit();
                    Log.Info("TRANSACTION COMMIT name=" + name + " status=" + commitStatus);
                    if (commitStatus != TransactionStatus.Committed)
                        throw new InvalidOperationException("Transaction '" + name + "' did not commit. Status: " + commitStatus);
                    return true;
                }
                catch (Exception ex)
                {
                    try
                    {
                        if (transaction.GetStatus() == TransactionStatus.Started)
                        {
                            TransactionStatus rollbackStatus = transaction.RollBack();
                            Log.Info("TRANSACTION ROLLBACK name=" + name + " reason=exception status=" + rollbackStatus);
                            if (rollbackStatus != TransactionStatus.RolledBack)
                                Log.Error(new InvalidOperationException("Rollback for transaction '" + name + "' returned status " + rollbackStatus + "."));
                        }
                    }
                    catch (Exception rollbackError)
                    {
                        Log.Error(new InvalidOperationException("Rollback failed for transaction '" + name + "'.", rollbackError));
                    }
                    Log.Error(new InvalidOperationException("TRANSACTION FAILED name=" + name, ex));
                    throw;
                }
                finally
                {
                    stopwatch.Stop();
                    string finalStatus;
                    try { finalStatus = transaction.GetStatus().ToString(); }
                    catch { finalStatus = "UNKNOWN"; }
                    Log.Info("TRANSACTION END name=" + name + " status=" + finalStatus +
                        " elapsed_ms=" + stopwatch.ElapsedMilliseconds + " document=" + Doc.Title);
                }
            }
        }
    }
}