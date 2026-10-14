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
        Vector2 commanderScroll;

        // Selection uses native colliders and highlighting. Orders never mutate the guest simulation.
        void GuestMapInput()
        {
            if(panel || choosingCommander || BlocksInput || CampaignEditor.BlocksInput || !authenticated || snapshot==null) return;
            if(PointerOverReplicaUi())return;
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
            if(!IsReplica || !authenticated) return;
            GUI.Box(new Rect(8,116,Math.Min(Screen.width-16,950),54),
                (guestSelection==null?"Select a formation on the native map.":guestSelection.name+" | Right-click terrain to request movement.")+"\n"+(string.IsNullOrEmpty(orderStatus)?message:orderStatus));
            if(guestSelection!=null && GUI.Button(new Rect(8,174,140,28),"Stop formation")) SubmitGuestOrder("stop",Vector3.zero);
            if(guestSelection!=null && GUI.Button(new Rect(154,174,180,28),"Replace commander"))choosingCommander=!choosingCommander;
            if(choosingCommander && guestSelection!=null) {
                GUILayout.BeginArea(new Rect(8,208,520,Math.Min(440,Screen.height-240)),GUI.skin.box);
                GUILayout.Label("Request a commander — host validates availability and rank");
                if(GUILayout.Button("Cancel"))choosingCommander=false;
                commanderScroll=GUILayout.BeginScrollView(commanderScroll);
                foreach(int id in EditorFormationOperations.Commanders(guestSide,guestSelection.unittyp,guestSelection)) {
                    var c=GameVars.commander[id];
                    if(GUILayout.Button(CampaignEditorData.OfficerName(id)+" | Experience "+c.exp.ToString("0.0"))) {
                        SubmitGuestOrder("commander",new Vector3(id,0,0));choosingCommander=false;
                    }
                }
                GUILayout.EndScrollView();GUILayout.EndArea();
            }
        }
    }
}
