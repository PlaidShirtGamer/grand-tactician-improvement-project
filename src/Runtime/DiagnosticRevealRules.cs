namespace GTIP
{
    internal static class DiagnosticRevealRules
    {
        internal static bool Include(int player, int faction, bool active, int mapTier) =>
            player >= 0 && player <= 1 && faction >= 0 && faction <= 1 && faction != player && active && mapTier > 0;
    }
}
