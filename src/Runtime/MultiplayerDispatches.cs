using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GTIP
{
    public sealed class MpDispatchReferences
    {
        public int Regiment, Sender, ObjectRegiment, ObjectBattle, AutoOpenSerial;
        public float X, Y, Z;
    }

    public sealed partial class MultiplayerCampaign
    {
        readonly Dictionary<int,MpStateRow[]> dispatchArchive=new Dictionary<int,MpStateRow[]>();
        readonly HashSet<int> hiddenDispatches=new HashSet<int>();
        readonly Dictionary<int,int> dispatchOpenSerials=new Dictionary<int,int>();
        readonly MpDispatchOpens dispatchOpens=new MpDispatchOpens();
        int dispatchOpenSerial;
        void ClearDispatchState(){dispatchArchive.Clear();hiddenDispatches.Clear();dispatchOpenSerials.Clear();dispatchOpens.Clear();dispatchOpenSerial=0;}
        static Messages Dispatches => GameObject.Find("UI")?.GetComponent<Messages>();
        int DispatchReference(Regiment unit) => unit != null && ids.TryGetValue(unit, out var id) ? id : 0;
        void CaptureDispatches(List<MpStateRow> rows)
        {
            var messages = Dispatches;
            if (messages == null) return;
            foreach (var m in messages.message)ArchiveDispatch(m);
            foreach(var pair in dispatchArchive.OrderBy(p=>p.Key))rows.AddRange(pair.Value);
        }
        void ArchiveDispatch(Messages.Message m)
        {
                dispatchArchive[m.number]=new[]{DataRow(7,m.number,m),DataRow(8, m.number, new MpDispatchReferences {
                    Regiment=DispatchReference(m.regref), Sender=DispatchReference(m.sender),
                    ObjectRegiment=DispatchReference(m.objref == null ? null : m.objref.GetComponent<Regiment>()),
                    ObjectBattle=BattleReference(m.objref == null ? null : m.objref.GetComponent<BattleMonument>()),
                    AutoOpenSerial=dispatchOpenSerials.TryGetValue(m.number,out var serial)?serial:0,
                    X=m.targetdestination.x, Y=m.targetdestination.y, Z=m.targetdestination.z
                })};
        }
        public static void ReplicaDispatchOpen(Messages target,int messageid,bool movetoposition,bool removescreenmassage)
        {
            NativeAccess.Call(target,"GTIP_Original_ReplicaDispatchOpen",messageid,movetoposition,removescreenmassage);
            var mp=instance;
            // ImportBattleResults explicitly opens the just-created major battle
            // result, without moving the camera. Capture that event rather than
            // guessing from unread state or reopening historical result messages.
            // Manual log/reopen operations remain local to the player using them.
            if(mp==null || !mp.host || mp.transport==null || mp.phase!="live" || !mp.authenticated ||
                messageid!=-1 || movetoposition || removescreenmassage || target.message.Count==0)return;
            var message=target.message[target.message.Count-1];
            if(message.type!=Messages.msg_combatresult && message.type!=Messages.msg_navalcombatresult)return;
            mp.dispatchOpenSerials[message.number]=checked(++mp.dispatchOpenSerial);
            mp.ArchiveDispatch(message);
            mp.LogDiagnostic("Native battle-result dispatch opened: number="+message.number+"; serial="+mp.dispatchOpenSerial);
        }
        public static void ReplicaRemoveDispatch(Messages target,int index)
        {
            var mp=instance;
            if(mp!=null && mp.transport!=null && index>=0 && index<target.message.Count) {
                if(mp.host)mp.ArchiveDispatch(target.message[index]);
                else mp.hiddenDispatches.Add(target.message[index].number);
            }
            NativeAccess.Call(target,"GTIP_Original_ReplicaRemoveDispatch",index);
        }
        void ApplyDispatches(MpStateRow[] rows)
        {
            var messages = Dispatches;
            if (messages == null) throw new InvalidOperationException("Native dispatch controller is unavailable.");
            var references=rows.Where(r=>r.Kind==8).ToDictionary(r=>r.Id);
            var incoming=rows.Where(r=>r.Kind==7).ToArray();
            if (incoming.Length != references.Count || incoming.Any(r=>!references.ContainsKey(r.Id)))
                throw new InvalidDataException("Incomplete dispatch state.");
            var existing=messages.message.ToDictionary(m=>m.number);
            var keep=new HashSet<int>(incoming.Select(r=>r.Id));
            // Remove native screen notifications through their own cleanup path.
            for(int i=messages.message.Count-1;i>=0;i--)
                if(!keep.Contains(messages.message[i].number)) messages.RemoveMessage(i);
            int added=0;
            foreach(var row in incoming) {
                var refs=new MpDispatchReferences(); DataSchemas[8].Apply(refs,references[row.Id].Values);
                bool hidden=hiddenDispatches.Contains(row.Id);
                dispatchOpens.Observe(row.Id,refs.AutoOpenSerial,hidden);
                if(hidden)continue;
                bool isNew=!existing.TryGetValue(row.Id,out var m);
                if(isNew) m=new Messages.Message();
                bool shown=m.wasshownonscreen,read=m.read;
                DataSchemas[7].Apply(m,row.Values);
                m.read=isNew?false:read;
                if(m.number!=row.Id)throw new InvalidDataException("Dispatch identity mismatch.");
                m.regref=units.TryGetValue(refs.Regiment,out var regiment)?regiment:null;
                m.sender=units.TryGetValue(refs.Sender,out var sender)?sender:null;
                if(refs.ObjectBattle!=0)m.objref=BattleById(refs.ObjectBattle)?.gameObject;
                else if(refs.ObjectRegiment!=0)m.objref=units.TryGetValue(refs.ObjectRegiment,out var obj)?obj.gameObject:null;
                m.targetdestination=new Vector3(refs.X,refs.Y,refs.Z);
                m.wasshownonscreen=shown;
                if(isNew) { messages.message.Add(m); added++; NativeAccess.Call(messages,"ShowMessageOnScreen",messages.message.Count-1); }
            }
            int opened=dispatchOpens.Take();
            if(opened>=0) {
                int index=messages.message.FindIndex(m=>m.number==opened);
                if(index>=0) {
                    // Native OpenMessage uses the replicated content and the local
                    // read flag. It has no dependency on a host-only battle monument.
                    messages.OpenMessage(index);
                    LogDiagnostic("Host battle-result dispatch displayed: number="+opened);
                }
            }
            NativeAccess.Set(messages,"messagesunread",messages.message.Count(m=>!m.read));
            Messages.runningnumber=Math.Max(Messages.runningnumber,incoming.Length==0?1:incoming.Max(r=>r.Id)+1);
            if(added>0)LogDiagnostic("Dispatch replication: "+added+" new; total="+messages.message.Count);
        }
        public static void ReplicaMessages(Messages target)
        {
            if(!IsReplica) { NativeAccess.Call(target,"GTIP_Original_ReplicaMessages"); return; }
            NativeAccess.Call(target,"UpdateScreenMessages");
            NativeAccess.Call(target,"UpdateMessagesCounter");
        }
        bool GuestDispatchNotification(List<RaycastResult> hits,PanelHandler handler)
        {
            bool left=Input.GetMouseButtonDown(0),right=Input.GetMouseButtonDown(1);
            if(!left && !right)return false;
            // Native ShowSymbol registers notifications in symbol, not in
            // multiplepanel. The tooltip may live on a child of the named symbol.
            // Only the frontmost UI hit is eligible, so covered notices stay inert.
            var hit=hits.FirstOrDefault(h=>h.module is UnityEngine.UI.GraphicRaycaster).gameObject;
            if(hit==null)return false;
            var symbol=handler.symbol.FirstOrDefault(p=>p.go!=null && p.go.activeInHierarchy && hit.transform.IsChildOf(p.go.transform));
            var messages=Dispatches;
            if(symbol==null || messages==null)return false;
            var message=messages.message.FirstOrDefault(m=>m.number==symbol.uniqueid);
            // Map rolling text also uses ShowSymbol and overlapping numeric icon
            // IDs. Require the complete native dispatch identity before routing.
            if(message==null || !MpDispatchSymbols.Matches(symbol.go.name,message.type,message.symboloffset) ||
                symbol.secondaryreference!=(message.sender!=null?message.sender.gameObject:null) ||
                symbol.referencego!=(message.regref!=null?message.regref.gameObject:null))return false;
            messages.InteractScreenMessage(message.number,right?2:1);
            LogDiagnostic("Dispatch notification "+(right?"dismissed":"opened")+": number="+message.number);
            return true;
        }
    }
}
