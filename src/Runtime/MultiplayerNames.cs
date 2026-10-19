using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        sealed class NameEdit {public int Id;public string Action,Authority,Pending,Draft;public float Until;public bool Writing,WasFocused;}
        readonly Dictionary<TMP_InputField,NameEdit> nameEdits=new Dictionary<TMP_InputField,NameEdit>();
        void SubmitName(NameEdit edit,string value)
        {
            if(!IsReplica || edit.Writing || edit.Id<=0 || string.IsNullOrWhiteSpace(value) || value==edit.Authority || value==edit.Pending ||
                value.Length>100 || value.IndexOfAny(new[]{'\r','\n','\0'})>=0)return;
            edit.Pending=value;edit.Draft=null;edit.Until=Time.realtimeSinceStartup+3;
            RequestNative(edit.Action,edit.Id,0,value);LogDiagnostic("Guest name submitted: "+edit.Action+", unit="+edit.Id);
        }
        void DriveName(TMP_InputField field,int id,string action,string authority)
        {
            if(field==null || !field.gameObject.activeInHierarchy)return;
            if(!nameEdits.TryGetValue(field,out var edit)) {
                edit=new NameEdit();nameEdits[field]=edit;var state=edit;
                field.onValueChanged.AddListener(value=> {if(IsReplica && !state.Writing && field.isFocused)state.Draft=value;});
                field.onEndEdit.AddListener(value=>SubmitName(state,value));
            }
            if(edit.Id!=id || edit.Action!=action){edit.Pending=null;edit.Draft=null;edit.WasFocused=false;edit.Id=id;edit.Action=action;}
            edit.Authority=authority;
            if(edit.Pending==authority || Time.realtimeSinceStartup>edit.Until)edit.Pending=null;
            // Some legacy panels hide/rebind without delivering onEndEdit. Preserve a real
            // edit through that focus transition instead of replacing it with host text.
            if(edit.WasFocused && !field.isFocused && edit.Draft!=null)SubmitName(edit,edit.Draft);
            edit.WasFocused=field.isFocused;
            if(!field.isFocused) {
                edit.Writing=true;
                try {field.text=edit.Pending??authority;}
                finally {edit.Writing=false;}
            }
            // Native name objects carry a visible TMP label as well as their input field.
            // Update both, as the original methods do, including while typing a draft.
            var label=field.GetComponent<TextMeshProUGUI>();
            if(label!=null)label.text=field.text;
        }
        public static void ReplicaUnitName(CampaignMainPanel target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaUnitName");return;}
            var unit=(Regiment)NativeAccess.Call(target,"GetUsedUnit",false,0);
            if(IsRecruitPreview(unit)){NativeAccess.Call(target,"GTIP_Original_ReplicaUnitName");return;}
            if(unit==null || !instance.ids.TryGetValue(unit,out int id))return;
            var h=instance.ui.panelhandler;var field=h.panel[h.cpanel_unitinfopanel].go.transform.Find("Background/078_UnitName")?.GetComponent<TMP_InputField>();
            instance.DriveName(field,id,"unit-name",unit.name);
        }
        public static void ReplicaFleetName(FleetInfoPanel target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaFleetName");return;}
            var fleet=NativeAccess.Get<Regiment>(target,"chosenfleet");if(fleet==null || !instance.ids.TryGetValue(fleet,out int id))return;
            var h=instance.ui.panelhandler;var field=h.panel[h.cpanel_mainpanelshipinfo].go.transform.Find("Background/CarryName/078_FleetName")?.GetComponent<TMP_InputField>();
            instance.DriveName(field,id,"unit-name",fleet.name);
        }
        public static void ReplicaShipName(FleetInfoPanel target)
        {
            var v=target.chosenvessel;
            if(!IsReplica || v==null || target.manualname.Length>0){NativeAccess.Call(target,"GTIP_Original_ReplicaShipName");return;}
            if(!instance.vesselIds.TryGetValue(v,out int id))return;
            var h=instance.ui.panelhandler;var field=h.panel[h.cpanel_mainpanelshipinfo].go.transform.Find("Background/CarryShipName/077_ShipName")?.GetComponent<TMP_InputField>();
            instance.DriveName(field,id,"ship-name",v.name);
        }
    }
}
