namespace GTIP
{
    public static class OfficerVisibility
    {
        // This predicate replaces only the native list's unassigned-dummy exclusion.
        public static bool HideDummy(GameVars.Commander officer) => OfficerVisibilityRules.Hide(
            SixLevelRuntime.Active, officer.usedummyphoto, officer.activeincampaign,
            officer.historicalbranch, officer.rank, officer.servicehistory);
    }
}
