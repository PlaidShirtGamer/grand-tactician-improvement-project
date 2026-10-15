using System;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        Regiment guestSelection;
        int guestSequence;
        string orderStatus = "";
        bool choosingCommander;

        // Selection uses native colliders and highlighting. Orders never mutate the guest simulation.
        void GuestMapInput()
        {
            if(panel || choosingCommander || GuestDialogOpen || NativeRecruitOpen || BlocksInput || CampaignEditor.BlocksInput || !authenticated || snapshot==null) return;
            if(PointerOverReplicaUi() || NativeModalOpen())return;
            if(Input.GetMouseButtonDown(0)) {
                var camera=Camera.main;
                if(camera==null) return;
                var ray=camera.ScreenPointToRay(Input.mousePosition);
                Regiment unit=null;
                foreach(var hit in Physics.RaycastAll(ray,100000f).OrderBy(h=>h.distance)) {
                    if(hit.collider==null)continue;
                    var go=hit.collider.gameObject;
                    if(!MpRules.NativeSelectionCollider(go.tag,go.name,GameVars.papermapvisible))continue;
                    // Campaign cards are selected through the native GraphicRaycaster above.
                    // A 3-D unit's company/range collider can span its whole command radius;
                    // only the visible symbol/name surface is eligible here.
                    Regiment candidate=null;
                    if(go.name=="UnitNameText" || (go.name=="UnitSymbol" && GameVars.papermapvisible))
                        candidate=go.transform.parent==null?null:go.transform.parent.GetComponent<Regiment>();
                    var surface=go.GetComponent<Renderer>();
                    if(surface==null || !surface.enabled || !surface.isVisible || !surface.bounds.IntersectRay(ray))continue;
                    if(candidate!=null && candidate.alliance==guestSide && ids.ContainsKey(candidate) && candidate.gameObject.activeInHierarchy) { unit=candidate; break; }
                }
                LogDiagnostic("Guest selection hit: "+(unit==null?"none":Id(unit)+" / "+unit.name));
                guestSelection=unit!=null && unit.alliance==guestSide && ids.ContainsKey(unit)?unit:null;
                ui.SelectUnit(null);
                if(guestSelection!=null) ui.SelectUnit(guestSelection.gameObject);
            }
            if(Input.GetMouseButtonDown(1) && guestSelection!=null) {
                var camera=Camera.main;
                var terrain=Terrain.activeTerrain;
                if(camera==null || terrain==null) { message="Campaign map is unavailable."; return; }
                var collider=terrain.GetComponent<TerrainCollider>();
                if(collider!=null && collider.Raycast(camera.ScreenPointToRay(Input.mousePosition),out var hit,100000f))
                    SubmitGuestOrder("move",hit.point);
            }
        }
        public static void ReplicaMoveUnit(BattleUnits engine,GameObject source,GameObject destination,bool skipdlccheck)
        {
            if(!IsReplica){NativeAccess.Call(engine,"GTIP_Original_ReplicaMoveUnit",source,destination,skipdlccheck);return;}
            if(instance==null || !instance.authenticated || source==null || destination==null)return;
            var unit=source.GetComponent<Regiment>();var parent=destination.GetComponent<Regiment>();
            if(unit==null || parent==null || !instance.ids.TryGetValue(unit,out int id) || !instance.ids.TryGetValue(parent,out int parentId))return;
            try {
                EditorFormationOperations.CheckMove(unit,parent);
                instance.Send(new MpMessage{kind="regroup",session=instance.session,sequence=++instance.guestSequence,unit=id,x=parentId});
                instance.orderStatus="Regroup request sent to host.";
            } catch(Exception e){instance.orderStatus=e.Message;}
        }

        void SubmitGuestOrder(string kind,Vector3 destination)
        {
            if(!authenticated || host || guestSelection==null || !ids.TryGetValue(guestSelection,out int id)) return;
            var state=snapshot?.units.FirstOrDefault(u=>u.id==id);
            if(state==null || (kind!="commander" && !state.controllable)) { orderStatus="The host reports this formation cannot receive movement orders."; return; }
            string identity=kind=="commander"?OfficerCommandIdentity((int)destination.x):"";
            Send(new MpMessage {kind=kind,session=session,sequence=++guestSequence,unit=id,x=destination.x,z=destination.z,text=identity,baseRevision=Baseline.Revision});
            orderStatus="Order "+guestSequence+" sent to host; awaiting confirmation.";
            LogDiagnostic(kind+" order "+guestSequence+" submitted for formation "+id);
        }
        static string OfficerCommandIdentity(int id)
        {
            if(id<0 || id>=GameVars.commander.Count)throw new InvalidOperationException("Officer no longer exists.");
            var c=GameVars.commander[id];
            string birth=string.Join(",",OfficerDates(c).Dates.Take(6).Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            return MpCheckpoint.Hash(System.Text.Encoding.UTF8.GetBytes(c.alliance+"|"+(c.firstname??"").Length+":"+c.firstname+"|"+c.name+"|"+birth));
        }

        void DrawGuestCommands()
        {
            // Selection, stop and commander controls belong to native game panels.
        }
    }
}
