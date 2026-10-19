namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        bool GuestRecruitPreviewAction(ButtonTooltip button)
        {
            if(!IsReplica || button==null || !button.isActiveAndEnabled || button.state==2 || ui==null || ui.panelhandler==null)return false;
            var main=NativeRecruitOpen?previewPanel:NativeAccess.Get<CampaignMainPanel>(ui.panelhandler,"campaignmainpanel");
            if(main==null || !main.isActiveAndEnabled || !button.transform.IsChildOf(main.transform))return false;
            bool preview=NativeRecruitOpen && nativePreview!=null && CampaignMainPanel.dummyregiment==nativePreview;
            var unit=preview?nativePreview:(Regiment)NativeAccess.Call(main,"GetUsedUnit",false,0);
            if(unit==null || (!preview && (CampaignMainPanel.dummyregiment!=null || unit.alliance!=guestSide || !ids.ContainsKey(unit))))return false;
            // Palette objects are siblings of UnitInfoPanel inside Panel_Main. Dispatch the
            // exact native controls directly, including controls omitted from panel.buttons.
            // Existing formations are edited on the host; only the dummy is edited locally.
            switch(button.name) {
                case "476_PrevSoldier":case "477_NextSoldier":
                    int direction=button.name=="476_PrevSoldier"?-1:1;
                    if(preview)unit.AdjustUniformSet(direction);else RequestNative("uniform-step",ids[unit],direction);
                    break;
                case "475_SwitchRecruitType":
                    if(!preview)return false;
                    unit.recruitingtype=unit.recruitingtype==0?1:0;break;
                case "947_RecolorUniform":main.ActivatePalette(!main.paletteactive);break;
                case "478_Left":case "479_Right":
                    var scrolling=button.GetComponentInParent<Palette>();
                    if(!IsUnitPalette(main,scrolling))return false;
                    scrolling.SwitchColorIndex(button.name=="478_Left"?-1:1);break;
                default:
                    if(!button.name.StartsWith("480_Color") || !int.TryParse(button.name.Substring(9),out int color))return false;
                    var palette=button.GetComponentInParent<Palette>();
                    if(!main.paletteactive || !IsUnitPalette(main,palette))return false;
                    palette.SelectColor(color);
                    var palettes=NativeAccess.Get<Palette[]>(main,"palette");int component=System.Array.IndexOf(palettes,palette);
                    if(preview)unit.componentcolor[component]=palette.GetSelectedColor();
                    else RequestNative("uniform-color",ids[unit],component,palette.currentcolorchosen.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    break;
            }
            // Native uniform selection retains state/policy eligibility. This refresh also
            // checks the chosen recruiting pool and disables Recruit when it is insufficient.
            main.UpdateUnitInfoPanel();
            LogDiagnostic("Guest uniform/recruit control: "+button.name+", preview="+preview+", uniform="+unit.uniformset+", recruits="+unit.recruitingtype);
            return true;
        }
        static bool IsUnitPalette(CampaignMainPanel main,Palette palette)
        {
            if(palette==null)return false;
            var palettes=NativeAccess.Get<Palette[]>(main,"palette");
            return palettes!=null && System.Array.IndexOf(palettes,palette)>=0 && System.Array.IndexOf(palettes,palette)<2;
        }
        bool RefreshRecruitPreviewRosterUi(CampaignMainPanel target)
        {
            if(!IsReplica || !NativeRecruitOpen || target!=previewPanel)return false;
            // UpdateOOBs(true,true) reinitializes RecruitingList through its filters,
            // clearing chosenstate and interrupting an in-progress guest recruitment.
            NativeAccess.Get<RecruitingList>(target,"recruitinglist")?.UpdateList();
            target.UpdateUnitData();
            return true;
        }
    }
}
