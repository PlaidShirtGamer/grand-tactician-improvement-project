using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        sealed class NameEdit {public int Id;public string Action,Authority,Pending;public float Until;}
        readonly Dictionary<TMP_InputField,NameEdit> nameEdits=new Dictionary<TMP_InputField,NameEdit>();
        void DriveName(TMP_InputField field,int id,string action,string authority)
        {
            if(field==null || !field.gameObject.activeInHierarchy)return;
            if(!nameEdits.TryGetValue(field,out var edit)) {
                edit=new NameEdit();nameEdits[field]=edit;var state=edit;
                field.onEndEdit.AddListener(value=> {
                    if(!IsReplica || value==state.Authority || state.Id<=0)return;
                    state.Pending=value;state.Until=Time.realtimeSinceStartup+3;RequestNative(state.Action,state.Id,0,value);
                });
            }
            if(edit.Id!=id || edit.Action!=action){edit.Pending=null;edit.Id=id;edit.Action=action;}
            edit.Authority=authority;
            if(edit.Pending==authority || Time.realtimeSinceStartup>edit.Until)edit.Pending=null;
            if(!field.isFocused)field.text=edit.Pending??authority;
        }
        public static void ReplicaUnitName(CampaignMainPanel target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaUnitName");return;}
            var unit=(Regiment)NativeAccess.Call(target,"GetUsedUnit");if(unit==null || !instance.ids.TryGetValue(unit,out int id))return;
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
