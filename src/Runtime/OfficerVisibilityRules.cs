using System;
namespace GTIP
{
    internal static class OfficerVisibilityRules
    {
        internal static bool Hide(bool enabled, bool dummy, bool active, int branch, int rank, string history)
        {
            if (!enabled || !dummy || !active) return dummy;
            bool created = history != null && history.StartsWith("Created with GTIP Aide-de-Camp", StringComparison.Ordinal);
            bool landGeneral = branch >= -1 && branch <= 3 && rank >= 6 && rank <= 9;
            return !(created || landGeneral);
        }
    }
}
