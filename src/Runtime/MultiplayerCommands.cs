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

        // Selection uses native colliders and highlighting. Orders never mutate the guest simulation.
        void GuestMapInput()
        {
            if(panel || BlocksInput || CampaignEditor.BlocksInput || !authenticated || snapshot==null) return;
            if(Input.mousePosition.y>Screen.height-210 || Input.mousePosition.y<80) return;
            if(Input.GetMouseButtonDown(0)) {
                var camera=Camera.main;
                if(camera==null) return;
                Regiment unit=null;
                foreach(var hit in Physics.RaycastAll(camera.ScreenPointToRay(Input.mousePosition),100000f).OrderBy(h=>h.distance)) {
                    var go=ui.GetUnitFromCollider(hit,true,false);
                    var candidate=go==null?hit.collider.GetComponentInParent<Regiment>():go.GetComponent<Regiment>();
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

        void SubmitGuestOrder(string kind,Vector3 destination)
        {
            if(!authenticated || host || guestSelection==null || !ids.TryGetValue(guestSelection,out int id)) return;
            var state=snapshot?.units.FirstOrDefault(u=>u.id==id);
            if(state==null || !state.controllable) { orderStatus="The host reports this formation cannot receive movement orders."; return; }
            Send(new MpMessage {kind=kind,session=session,sequence=++guestSequence,unit=id,x=destination.x,z=destination.z});
            orderStatus="Order "+guestSequence+" sent to host; awaiting confirmation.";
            LogDiagnostic(kind+" order "+guestSequence+" submitted for formation "+id);
        }

        void DrawGuestCommands()
        {
            if(!IsReplica || !authenticated) return;
            GUI.Box(new Rect(8,116,Math.Min(Screen.width-16,950),54),
                (guestSelection==null?"Select a formation on the native map.":guestSelection.name+" | Right-click terrain to request movement.")+"\n"+(string.IsNullOrEmpty(orderStatus)?message:orderStatus));
            if(guestSelection!=null && GUI.Button(new Rect(8,174,140,28),"Stop formation")) SubmitGuestOrder("stop",Vector3.zero);
        }
    }
}
