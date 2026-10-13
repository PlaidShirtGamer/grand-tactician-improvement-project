namespace GTIP
{
    public static class OfficerVisibility
    {
        public static bool RecruitableOfficerState(GameVars.Nation state) => SixLevelRuntime.Active || state.recruitable;
        public static bool Suitable(GameVars.Commander officer, Regiment unit)
        {
            if (!officer.CheckSuitableRank(unit)) return false;
            if (!SixLevelRuntime.Active || unit == null) return true;
            int type = unit.overridesymbol >= 10 ? unit.overridesymbol : unit.unittyp;
            if (unit.isgarrisonbasicunit || unit.garrisonreference != null) type = GameVars.standardunittypegarrisons;
            return officer.rank >= GameVars.GetMinRankForUnittyp(type, officer.alliance);
        }
        // This predicate replaces only the native list's unassigned-dummy exclusion.
        public static bool HideDummy(GameVars.Commander officer) => OfficerVisibilityRules.Hide(
            SixLevelRuntime.Active, officer.usedummyphoto, officer.activeincampaign,
            officer.historicalbranch, officer.rank, officer.servicehistory, officer.westpoint || officer.veteran || officer.political);
    }
}
