using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        // Campaign records only. Unity references, AI plans and rendering state never go through reflection.
        static MpStateSchema[] dataSchemas;
        static MpStateSchema[] DataSchemas => dataSchemas ?? (dataSchemas=new[] {
            MpStateSchema.Scalars(typeof(GameVars.Alliance)),
            MpStateSchema.Scalars(typeof(GameVars.Nation),"supportinfluencelast"),
            MpStateSchema.Scalars(typeof(GameVars.Commander)),
            new MpStateSchema(typeof(Regiment),new[] {
                "commander","strength","sick","losses","morale","training","experience","fatigue","ammo","weapon","nation","overridesymbol","uniformset","servicehistory","abrvname","battleflag",
                "conditionmen","conditionhorses","conditionguns","readiness","intel","fightingspirit","cohesion",
                "reinforcementpriority","reinforcementtype","reformtime","finishedrecruitment","recruitingtype",
                "latestreinforcements","reinforcementsneeded","contractlength","state","numberofbattles",
                "campaignstance","campaignstanceordered","cavalryorders","cavalryordersordered","fleetorders",
                "transfertime","entrenchmentlevel","supplystate","supplyinstock","supplychange",
                "groupstrength","groupstrengthactive","groupstrengthdirect","groupstrengthaigroup",
                "groupstrengthperunittyp","groupstrengthperunittypcampaigndirect","groupcountperunittype",
                "groupguns","groupmorale","groupfightingspirit","grouplosses","groupwounded","groupkilled",
                "groupmissing","groupcaptured","groupsick","groupammo","groupcohesion","grouptraining",
                "groupstate","groupexperience","groupcondition","groupfatigue","groupsupplystate" }),
            MpStateSchema.Scalars(typeof(MpFormationState)),MpStateSchema.Scalars(typeof(MpOfficerDates)),MpStateSchema.Scalars(typeof(MpArmyGroupState)),
            MpStateSchema.Scalars(typeof(Messages.Message)),MpStateSchema.Scalars(typeof(MpDispatchReferences)),MpStateSchema.Scalars(typeof(MpPolicyState))
        });
        byte[] outgoingData;
        MemoryStream incomingData;
        int dataSerial, incomingDataSerial, dataLength, dataOffset;
        string dataHash;
        float lastDataSend, dataStarted;
        MpStateBaseline dataBaseline;
        MpStateRow[] pendingDataRows;
        string pendingStateHash;
        int incomingBaseRevision;
        string incomingStateHash;
        double lastCaptureMs,lastPrepareMs;
        sealed class PreparedData {public MpStateRow[] Rows;public byte[] Bytes;public string Hash;public int Revision,BaseRevision,Changed;public double Milliseconds;}
        System.Threading.Tasks.Task<PreparedData> prepareTask;
        System.Threading.Tasks.Task commitTask;
        sealed class ReceivedData {public MpStateRow[] Rows,Changes;public int Bytes;}
        System.Threading.Tasks.Task<ReceivedData> receiveTask;
        MpStateBaseline Baseline => dataBaseline ?? (dataBaseline=new MpStateBaseline(DataSchemas));

        int ReferenceId(Regiment unit)
        { if(unit==null)return 0; if(!ids.TryGetValue(unit,out int id))throw new InvalidDataException("Officer references an unknown formation.");return id; }
        MpStateRow[] CaptureData()
        {
            if(host)RefreshHostIds();
            var rows=new List<MpStateRow>();
            for(int i=0;i<GameVars.alliance.Length;i++) if(GameVars.alliance[i]!=null) rows.Add(DataRow(0,i,GameVars.alliance[i]));
            for(int i=0;i<GameVars.nation.Length;i++) if(GameVars.nation[i]!=null) rows.Add(DataRow(1,i,GameVars.nation[i]));
            for(int i=0;i<GameVars.commander.Count;i++) if(GameVars.commander[i]!=null) {
                var c=GameVars.commander[i]; var row=DataRow(2,i,c);
                row.Current=ReferenceId(c.currentcommand); row.Last=ReferenceId(c.lastcommand);rows.Add(row);
                rows.Add(DataRow(5,i,OfficerDates(c)));
            }
            foreach(var pair in units.OrderBy(p=>p.Key)) {var row=DataRow(3,pair.Key,pair.Value);row.Name=pair.Value.name;rows.Add(row);rows.Add(DataRow(4,pair.Key,FormationState(pair.Value)));}
            foreach(var pair in groups) {
                var g=pair.Value;var state=new MpArmyGroupState{Commander=g.GetCommanderID(),Override=g.overridelev,Members=g.attachedunits.Where(u=>u!=null).Select(ReferenceId).ToArray(),Color=ColorData(g.GetColor())};
                var row=DataRow(6,pair.Key,state);row.Name=g.name;rows.Add(row);
            }
            CaptureDispatches(rows);
            CapturePolicies(rows);
            return rows.ToArray();
        }
        static MpStateRow DataRow(int kind,int id,object target) => new MpStateRow {Kind=kind,Id=id,Values=DataSchemas[kind].Capture(target)};
        object DataTarget(MpStateRow row)
        {
            switch(row.Kind) {
                case 0: return row.Id<GameVars.alliance.Length?GameVars.alliance[row.Id]:null;
                case 1: return row.Id<GameVars.nation.Length?GameVars.nation[row.Id]:null;
                case 2: return row.Id<GameVars.commander.Count?GameVars.commander[row.Id]:null;
                case 3: return units.TryGetValue(row.Id,out var unit)?unit:null;
                default: return null;
            }
        }
        void DataTick()
        {
            if((outgoingData!=null || incomingData!=null || receiveTask!=null || commitTask!=null) && Time.realtimeSinceStartup-dataStarted>30)throw new TimeoutException("Campaign data synchronization timed out.");
            if(!host && commitTask!=null) {
                if(!commitTask.IsCompleted)return;
                commitTask.GetAwaiter().GetResult();commitTask=null;
                Send(new MpMessage{kind="data-applied",session=session,sequence=incomingDataSerial,stateHash=Baseline.Hash});
            }
            if(!host && receiveTask!=null) {
                if(!receiveTask.IsCompleted)return;
                var received=receiveTask.GetAwaiter().GetResult();receiveTask=null;
                var applyTimer=System.Diagnostics.Stopwatch.StartNew();
                bool incremental=TryApplyScalarDelta(received.Changes);
                if(!incremental) {ApplyPolicies(received.Rows);ApplyLifecycleData(received.Rows);}
                if(received.Changes.Any(r=>r.Kind==7 || r.Kind==8))ApplyDispatches(received.Rows);
                VerifyAppliedScalars(incremental?received.Changes:received.Rows);
                if(incomingDataSerial==1 || incomingDataSerial%30==0)LogDiagnostic("Replication apply: revision="+incomingDataSerial+", changed="+received.Changes.Length+", incremental="+incremental+", mainThreadMs="+applyTimer.Elapsed.TotalMilliseconds.ToString("F1"));
                var appliedBaseline=Baseline;var rows=received.Rows;int revision=incomingDataSerial;string hash=incomingStateHash;
                commitTask=System.Threading.Tasks.Task.Run(()=>appliedBaseline.Commit(revision,rows,hash));
            }
            if(host && commitTask!=null) {
                if(!commitTask.IsCompleted)return;
                commitTask.GetAwaiter().GetResult();commitTask=null;
                outgoingData=null;pendingDataRows=null;pendingStateHash=null;
                if(!RosterChanged())Send(Capture());
            }
            if(host && prepareTask!=null) {
                if(!prepareTask.IsCompleted)return;
                var prepared=prepareTask.GetAwaiter().GetResult();prepareTask=null;
                if(prepared==null)return;
                dataSerial=prepared.Revision;pendingDataRows=prepared.Rows;pendingStateHash=prepared.Hash;
                outgoingData=prepared.Bytes;dataOffset=0;lastPrepareMs=prepared.Milliseconds;dataStarted=Time.realtimeSinceStartup;
                if(dataSerial==1 || dataSerial%30==0)LogDiagnostic("Replication prepare: revision="+dataSerial+", records="+pendingDataRows.Length+", changed="+prepared.Changed+", bytes="+outgoingData.Length+", captureMs="+lastCaptureMs.ToString("F1")+", workerMs="+lastPrepareMs.ToString("F1"));
                Send(new MpMessage{kind="data-offer",session=session,sequence=dataSerial,baseRevision=prepared.BaseRevision,stateHash=pendingStateHash,unit=outgoingData.Length,text=MpCheckpoint.Hash(outgoingData)});
                return;
            }
            if(!host || outgoingData!=null || Time.realtimeSinceStartup-lastDataSend<0.25f)return;
            lastDataSend=Time.realtimeSinceStartup;dataStarted=lastDataSend;
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var captured=CaptureData();lastCaptureMs=timer.Elapsed.TotalMilliseconds;
            var baseline=Baseline;var schemas=DataSchemas;
            // Capture owns cloned arrays. This worker sees DTOs only, never Unity objects.
            // One preparation/commit may run at a time; no capture races a baseline mutation.
            prepareTask=System.Threading.Tasks.Task.Run(()=> {
                var work=System.Diagnostics.Stopwatch.StartNew();
                var changes=baseline.Changes(captured);if(changes.Length==0)return null;
                int revision=checked(baseline.Revision+1);
                var rows=baseline.Prepare(revision,baseline.Revision,changes);
                return new PreparedData{Rows=rows,Hash=baseline.Fingerprint(rows),Bytes=MpStateCodec.Encode(schemas,changes),Revision=revision,BaseRevision=baseline.Revision,Changed=changes.Length,Milliseconds=work.Elapsed.TotalMilliseconds};
            });
        }
        bool DataMessage(MpMessage value)
        {
            if(value.kind=="data-applied") {
                if(!host || commitTask!=null || outgoingData==null || dataOffset!=outgoingData.Length || value.sequence!=dataSerial || value.stateHash!=pendingStateHash)
                    throw new InvalidDataException("Invalid campaign application receipt.");
                if(dataSerial==1 || dataSerial%30==0)LogDiagnostic("Replication receipt: revision="+dataSerial+", elapsedMs="+((Time.realtimeSinceStartup-dataStarted)*1000).ToString("F1"));
                var baseline=Baseline;var rows=pendingDataRows;var hash=pendingStateHash;int revision=dataSerial;
                commitTask=System.Threading.Tasks.Task.Run(()=>baseline.Commit(revision,rows,hash));
                return true;
            }
            if(value.kind=="data-next") {
                if(!host || outgoingData==null || value.sequence!=dataSerial || value.unit!=dataOffset)throw new InvalidDataException("Unexpected campaign data acknowledgement.");
                if(dataOffset==outgoingData.Length)throw new InvalidDataException("Missing campaign application receipt.");
                int n=Math.Min(MpCheckpoint.ChunkBytes,outgoingData.Length-dataOffset);var bytes=new byte[n];Buffer.BlockCopy(outgoingData,dataOffset,bytes,0,n);
                Send(new MpMessage{kind="data-chunk",session=session,sequence=dataSerial,unit=dataOffset,blob=bytes});dataOffset+=n;return true;
            }
            if(value.kind=="data-offer") {
                if(host || incomingData!=null || receiveTask!=null || commitTask!=null || value.sequence<=incomingDataSerial || value.unit<1 || value.unit>MpStateCodec.MaxBytes)throw new InvalidDataException("Invalid campaign data offer.");
                if(value.baseRevision!=Baseline.Revision || value.sequence!=checked(Baseline.Revision+1) || string.IsNullOrEmpty(value.stateHash))throw new InvalidDataException("Unexpected campaign base revision.");
                incomingBaseRevision=value.baseRevision;incomingStateHash=value.stateHash;
                incomingDataSerial=value.sequence;dataLength=value.unit;dataHash=value.text;incomingData=new MemoryStream();dataStarted=Time.realtimeSinceStartup;
                Send(new MpMessage{kind="data-next",session=session,sequence=incomingDataSerial,unit=0});return true;
            }
            if(value.kind=="data-chunk") {
                if(host || incomingData==null || value.sequence!=incomingDataSerial || value.unit!=incomingData.Length || value.blob==null || value.blob.Length==0 || incomingData.Length+value.blob.Length>dataLength)
                    throw new InvalidDataException("Invalid campaign data chunk.");
                incomingData.Write(value.blob,0,value.blob.Length);
                int received=(int)incomingData.Length;
                if(received==dataLength) {
                    var bytes=incomingData.ToArray();string hash=dataHash,stateHash=incomingStateHash;
                    int revision=incomingDataSerial,baseRevision=incomingBaseRevision;
                    var schemas=DataSchemas;var baseline=Baseline;
                    incomingData.Dispose();incomingData=null;
                    receiveTask=System.Threading.Tasks.Task.Run(()=> {
                        if(MpCheckpoint.Hash(bytes)!=hash)throw new InvalidDataException("Campaign data checksum mismatch.");
                        var changes=MpStateCodec.Decode(schemas,bytes);
                        return new ReceivedData{Rows=baseline.Prepare(revision,baseRevision,changes,stateHash),Changes=changes,Bytes=bytes.Length};
                    });
                    return true;
                }
                Send(new MpMessage{kind="data-next",session=session,sequence=incomingDataSerial,unit=received});return true;
            }
            return false;
        }
        void ClearData(bool restore)
        {
            outgoingData=null;incomingData?.Dispose();incomingData=null;dataSerial=0;incomingDataSerial=0;lastDataSend=0;
            dataBaseline=null;pendingDataRows=null;pendingStateHash=null;incomingStateHash=null;incomingBaseRevision=0;
            prepareTask=null;commitTask=null;receiveTask=null;
        }
        bool TryApplyScalarDelta(MpStateRow[] changes)
        {
            // Structural transactions still use the full validated lifecycle adapter. Ordinary
            // scalar updates must not reparent every unit or rebuild all officer dates/groups.
            if(!hasLiveData || changes.Any(r=>r.Deleted || r.Kind==4 || r.Kind==6 || r.Kind==9 ||
                (r.Kind<=3 && DataTarget(r)==null) || (r.Kind==5 && r.Id>=GameVars.commander.Count)))return false;
            foreach(var row in changes.Where(r=>r.Kind==2))
                if((row.Current!=0 && !units.ContainsKey(row.Current)) || (row.Last!=0 && !units.ContainsKey(row.Last)))throw new InvalidDataException("Unknown officer command reference.");
            foreach(var row in changes.Where(r=>r.Kind==5)) {
                var dates=ReadData<MpOfficerDates>(row);
                if(dates.Promotions<0 || dates.Promotions>32 || dates.Dates==null || dates.Dates.Length!=(4+dates.Promotions)*6)throw new InvalidDataException("Invalid officer dates.");
            }
            ApplyPolicies(changes);
            foreach(var row in changes.Where(r=>r.Kind<=3)) {
                var target=DataTarget(row);DataSchemas[row.Kind].Apply(target,row.Values);
                if(row.Kind==2){var c=(GameVars.Commander)target;c.currentcommand=row.Current==0?null:units[row.Current];c.lastcommand=row.Last==0?null:units[row.Last];}
                if(row.Kind==3)((Regiment)target).name=row.Name;
            }
            foreach(var row in changes.Where(r=>r.Kind==5))ApplyOfficerDates(GameVars.commander[row.Id],ReadData<MpOfficerDates>(row));
            return true;
        }
        void VerifyAppliedScalars(MpStateRow[] rows)
        {
            // Read native objects back, not the receive buffer. This covers registered scalar/
            // array adapters; it deliberately makes no claim about unregistered native state.
            foreach(var row in rows.Where(r=>r.Kind<=3)) {
                var target=DataTarget(row);
                if(target==null)throw new InvalidDataException("Applied native object missing: "+row.Kind+":"+row.Id);
                string field=MpStateBaseline.FirstDifference(DataSchemas[row.Kind],row.Values,DataSchemas[row.Kind].Capture(target));
                if(field!=null)throw new InvalidDataException("Native state divergence at revision "+incomingDataSerial+": "+row.Kind+":"+row.Id+" field="+field);
            }
        }
    }
}
