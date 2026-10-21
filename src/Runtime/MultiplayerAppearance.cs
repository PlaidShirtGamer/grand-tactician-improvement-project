using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        public static GameObject ReplicaUniformPanel(GameObject nativePanel)
        {
            // UniformSet uses Find("UI/Panel_Main") even when only calculating the
            // eligible uniform list. Find excludes inactive objects; remote host orders
            // must use the already initialized panel without opening the host's UI.
            if(nativePanel!=null || !LiveSession || instance.ui==null || instance.ui.panelhandler==null)return nativePanel;
            var cached=NativeAccess.Get<CampaignMainPanel>(instance.ui.panelhandler,"campaignmainpanel");
            return cached!=null && cached.gameObject!=null && cached.gameObject.name=="Panel_Main"?cached.gameObject:nativePanel;
        }
        sealed class PendingUniform {public int Value;public float Until;}
        readonly Dictionary<Regiment,PendingUniform> pendingUniforms=new Dictionary<Regiment,PendingUniform>();
        void PreviewExistingUniformStep(Regiment unit,int direction)
        {
            int authority=unit.uniformset;
            try {
                unit.uniformset=PreviewUniformValue(unit);
                unit.AdjustUniformSet(direction);
                pendingUniforms[unit]=new PendingUniform{Value=unit.uniformset,Until=UnityEngine.Time.realtimeSinceStartup+3};
            } finally {unit.uniformset=authority;}
        }
        int PreviewUniformValue(Regiment unit)
        {
            if(!pendingUniforms.TryGetValue(unit,out var pending))return unit.uniformset;
            if(pending.Value==unit.uniformset || UnityEngine.Time.realtimeSinceStartup>pending.Until){pendingUniforms.Remove(unit);return unit.uniformset;}
            return pending.Value;
        }
        bool ExecuteAppearanceAction(string action,MpMessage request,string detail)
        {
            if(action!="uniform-step" && action!="uniform-color")return false;
            if(!units.TryGetValue(request.unit,out var unit) || unit==null)
                throw new InvalidOperationException("Formation uniform unavailable: id="+request.unit+" is absent from the synchronized roster.");
            // Military management includes inactive subordinate regiments. Native palette
            // and uniform callbacks do not require an active campaign-map GameObject.
            if(unit.alliance!=guestSide || unit.unittyp>13)
                throw new InvalidOperationException("Formation uniform unavailable: id="+request.unit+", name="+unit.name+", type="+unit.unittyp+", side="+unit.alliance+", requestedSide="+guestSide+", active="+unit.gameObject.activeInHierarchy);
            int selectedColor=-1,selectedComponent=-1;
            if(action=="uniform-step") {
                if(request.x!=-1 && request.x!=1)throw new InvalidOperationException("Invalid uniform direction.");
                // The native operation chooses from this unit's policy/state-specific list.
                unit.AdjustUniformSet((int)request.x);
            } else {
                int component=(int)request.x;
                if(request.x!=component || component<0 || component>1 || !int.TryParse(detail,NumberStyles.None,CultureInfo.InvariantCulture,out int color) || color<0)
                    throw new InvalidOperationException("Invalid uniform color.");
                var value=Palette.GetColorFromIndex(color);
                var palette=NativeAccess.Get<Array>(typeof(Palette),"palettecolors");
                if(palette==null || color>=palette.Length)throw new InvalidOperationException("Uniform color is not in the native palette.");
                if(unit.componentcolor==null || unit.componentcolor.Length<2)throw new InvalidOperationException("Formation has no native uniform colors.");
                // Apply the same component change as the native palette; avoid resetting the
                // other component or introducing arbitrary colors outside the native palette.
                unit.componentcolor[component]=value;
                selectedColor=color;selectedComponent=component;
            }
            try {RefreshHostAppearance(unit,selectedComponent,selectedColor);}
            catch(Exception error){LogDiagnostic("Host appearance panel refresh failed after accepting unit="+request.unit+": "+error);}
            LogDiagnostic("Native guest appearance: unit="+request.unit+", action="+action+", type="+unit.unittyp+", active="+unit.gameObject.activeInHierarchy+", uniform="+unit.uniformset);
            return true;
        }
        void RefreshHostAppearance(Regiment unit,int component,int color)
        {
            if(IsReplica || ui==null || ui.panelhandler==null || ui.selectedunitreg!=unit || CampaignMainPanel.dummyregiment!=null)return;
            var main=NativeAccess.Get<CampaignMainPanel>(ui.panelhandler,"campaignmainpanel");
            if(main==null || !main.isActiveAndEnabled || NativeAccess.Get<Regiment>(main,"lastusedunit")!=unit)return;
            if(component>=0) {
                var palettes=NativeAccess.Get<Palette[]>(main,"palette");
                // Native UpdateSoldierPicture writes open palette selections into the
                // unit. Move that local selection to the accepted absolute palette index
                // before a redraw can write the host's stale selection back over it.
                if(palettes!=null && component<palettes.Length && palettes[component]!=null)palettes[component].currentcolorchosen=color;
            }
            main.UpdateUnitInfoPanel();
        }
        public static void ReplicaSoldierPicture(CampaignMainPanel target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaSoldierPicture");return;}
            var unit=(Regiment)NativeAccess.Call(target,"GetUsedUnit",false,0);
            if(unit==null || IsRecruitPreview(unit)){NativeAccess.Call(target,"GTIP_Original_ReplicaSoldierPicture");return;}
            int uniform=unit.uniformset;var original=unit.componentcolor;
            // Native rendering writes palette selections into Regiment and can normalize an
            // unsupported uniform ID. Render with a disposable copy, then retain host state.
            try {
                unit.uniformset=instance.PreviewUniformValue(unit);
                if(original!=null)unit.componentcolor=(Color[])original.Clone();
                NativeAccess.Call(target,"GTIP_Original_ReplicaSoldierPicture");
            } finally {unit.componentcolor=original;unit.uniformset=uniform;}
        }
    }
}
