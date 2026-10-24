using System;
using System.Collections.Generic;

namespace ElecKit.Core
{
    /// <summary>Revit-independent rule composition and exact displayed-value comparison for Smart Filter.</summary>
    public static class SmartFilterLogic
    {
        public static bool Matches(IList<bool> ruleResults, bool useAnd)
        {
            if (ruleResults == null) throw new ArgumentNullException(nameof(ruleResults));
            if (ruleResults.Count == 0) return false;

            for (int i = 0; i < ruleResults.Count; i++)
            {
                if (useAnd && !ruleResults[i]) return false;
                if (!useAnd && ruleResults[i]) return true;
            }

            return useAnd;
        }

        public static bool ValuesEqual(string actual, string expected)
        {
            return string.Equals(actual ?? string.Empty, expected ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}