public class Regiment { public int unittyp, overridesymbol = -1; public bool isgarrisonbasicunit; public object garrisonreference; }
public static class GameVars {
    public static int standardunittypegarrisons = 13;
    public static int GetMinRankForUnittyp(int type, int side) => type >= 16 ? 6 : 4;
    public class Nation { public bool recruitable; }
    public class Commander {
        public bool usedummyphoto, activeincampaign = true, westpoint, veteran, political;
        public int historicalbranch, rank, alliance; public string servicehistory = "";
        public bool nativeSuitable = true;
        public bool CheckSuitableRank(Regiment unit) => nativeSuitable;
    }
}
namespace GTIP { public static class SixLevelRuntime { public static bool Active = true; } }
