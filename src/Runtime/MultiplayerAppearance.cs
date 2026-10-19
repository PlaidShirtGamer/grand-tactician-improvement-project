using System;
using System.Globalization;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        bool ExecuteAppearanceAction(string action,MpMessage request,string detail)
        {
            if(action!="uniform-step" && action!="uniform-color")return false;
            if(!units.TryGetValue(request.unit,out var unit) || unit==null || unit.alliance!=guestSide || !unit.gameObject.activeInHierarchy || unit.unittyp>13)
                throw new InvalidOperationException("Formation uniform is unavailable.");
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
            }
            LogDiagnostic("Native guest appearance: unit="+request.unit+", action="+action);
            return true;
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
                if(original!=null)unit.componentcolor=(Color[])original.Clone();
                NativeAccess.Call(target,"GTIP_Original_ReplicaSoldierPicture");
            } finally {unit.componentcolor=original;unit.uniformset=uniform;}
        }
    }
}
