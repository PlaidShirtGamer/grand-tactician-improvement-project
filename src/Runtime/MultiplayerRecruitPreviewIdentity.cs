namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        public static bool IsRecruitPreview(Regiment unit) => instance!=null && unit!=null && !instance.ids.ContainsKey(unit) &&
            (instance.nativePreview==unit || instance.buildingPreview);
    }
}
