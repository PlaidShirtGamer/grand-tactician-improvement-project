using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        BuildingList buildingListView;
        RailroadList railroadListView;
        WeaponList[] weaponListViews;
        sealed class PendingPreference { public bool Value;public float Sent; }
        readonly Dictionary<int,PendingPreference> constructionPreferences=new Dictionary<int,PendingPreference>();
        bool weaponViewDirty;
        bool fleetViewDirty;
        void GuestPreference(CheckBox box,int key,bool authority)
        {
            if(box.HasChanged()) {
                constructionPreferences[key]=new PendingPreference{Value=box.isactive,Sent=Time.realtimeSinceStartup};
                RequestNative("construction-option",key,box.isactive?1:0);
            }
            bool shown=authority;
            if(constructionPreferences.TryGetValue(key,out var pending)) {
                if(pending.Value==authority || Time.realtimeSinceStartup-pending.Sent>3)constructionPreferences.Remove(key);
                else shown=pending.Value;
            }
            box.Check(shown);box.SetChangedFalse();
        }
        void DriveGuestNativeViews()
        {
            // Drive native input-reading views only after current-frame hover resolution.
            // Their simulation-bearing operations are separately wrapped as host intents.
            if(buildingListView==null)buildingListView=Resources.FindObjectsOfTypeAll<BuildingList>().FirstOrDefault(v=>v.gameObject.scene.IsValid());
            if(railroadListView==null)railroadListView=Resources.FindObjectsOfTypeAll<RailroadList>().FirstOrDefault(v=>v.gameObject.scene.IsValid());
            if(weaponListViews==null)weaponListViews=Resources.FindObjectsOfTypeAll<WeaponList>().Where(v=>v.gameObject.scene.IsValid()).ToArray();
            if(buildingListView!=null && buildingListView.isActiveAndEnabled)NativeAccess.Call(buildingListView,"GTIP_Original_ReplicaBuildingList");
            if(railroadListView!=null && railroadListView.isActiveAndEnabled)NativeAccess.Call(railroadListView,"GTIP_Original_ReplicaRailroadList");
            foreach(var v in weaponListViews)if(v!=null && v.isActiveAndEnabled) {
                if(!NativeAccess.Get<bool>(v,"initialized"))continue;
                if(weaponViewDirty){v.UpdateList();NativeAccess.Call(v,"UpdateManagementInfo");}
                NativeAccess.Call(v,"GTIP_Original_ReplicaWeaponList");
            }
            weaponViewDirty=false;
            RefreshOfficerView();
            if(fleetViewDirty && ui!=null && ui.panelhandler!=null && !ui.panelhandler.draganddropactive) {
                var h=ui.panelhandler;var main=NativeAccess.Get<CampaignMainPanel>(h,"campaignmainpanel");
                if(main!=null && h.panel[h.cpanel_mainpanel].state>0 && main.contentselection==1){main.ForceCheckFilterSelectionChange();LogDiagnostic("Native fleet and ship-pool lists refreshed after roster change.");}
                fleetViewDirty=false;
            }
        }
        void RefreshOfficerView()
        {
            if(!officerViewDirty || ui==null || ui.panelhandler==null)return;
            var h=ui.panelhandler;
            if(h.panel[h.cpanel_mainpanel].state<=0)return;
            var main=NativeAccess.Get<CampaignMainPanel>(h,"campaignmainpanel");
            if(main==null)return;
            NativeAccess.Call(main,"UpdateCommanderData");
            if(NativeAccess.Get<int>(main,"contentselection")==13)main.ForceCheckFilterSelectionChange();
            officerViewDirty=false;
        }
        public static void ReplicaBuildingList(BuildingList target){if(!IsReplica)NativeAccess.Call(target,"GTIP_Original_ReplicaBuildingList");}
        public static void ReplicaRailroadList(RailroadList target){if(!IsReplica)NativeAccess.Call(target,"GTIP_Original_ReplicaRailroadList");}
        public static void ReplicaWeaponList(WeaponList target){if(!IsReplica)NativeAccess.Call(target,"GTIP_Original_ReplicaWeaponList");}
        public static void ReplicaWeaponOptions(WeaponList target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaWeaponOptions");return;}
            instance.GuestPreference(target.Automanage,4,GameVars.automanageweapons);
        }
    }
}
