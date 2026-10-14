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
                "conditionmen","conditionhorses","conditionguns","readiness","intel","fightingspirit",
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
            MpStateSchema.Scalars(typeof(Messages.Message)),MpStateSchema.Scalars(typeof(MpDispatchReferences))
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
            if((outgoingData!=null || incomingData!=null) && Time.realtimeSinceStartup-dataStarted>30)throw new TimeoutException("Campaign data synchronization timed out.");
            if(!host || outgoingData!=null || Time.realtimeSinceStartup-lastDataSend<0.25f)return;
            lastDataSend=Time.realtimeSinceStartup;dataStarted=lastDataSend;
            var changes=Baseline.Changes(CaptureData());
            if(changes.Length==0)return;
            dataSerial=checked(Baseline.Revision+1);
            pendingDataRows=Baseline.Prepare(dataSerial,Baseline.Revision,changes);
            pendingStateHash=Baseline.Fingerprint(pendingDataRows);
            outgoingData=MpStateCodec.Encode(DataSchemas,changes);dataOffset=0;
            Send(new MpMessage {kind="data-offer",session=session,sequence=dataSerial,baseRevision=Baseline.Revision,stateHash=pendingStateHash,unit=outgoingData.Length,text=MpCheckpoint.Hash(outgoingData)});
        }
        bool DataMessage(MpMessage value)
        {
            if(value.kind=="data-applied") {
                if(!host || outgoingData==null || dataOffset!=outgoingData.Length || value.sequence!=dataSerial || value.stateHash!=pendingStateHash)
                    throw new InvalidDataException("Invalid campaign application receipt.");
                Baseline.Commit(dataSerial,pendingDataRows,pendingStateHash);
                outgoingData=null;pendingDataRows=null;pendingStateHash=null;
                if(dataSerial==1 || dataSerial%100==0)LogDiagnostic("Guest acknowledged campaign revision "+dataSerial+" hash="+Baseline.Hash);
                if(!RosterChanged())Send(Capture());return true;
            }
            if(value.kind=="data-next") {
                if(!host || outgoingData==null || value.sequence!=dataSerial || value.unit!=dataOffset)throw new InvalidDataException("Unexpected campaign data acknowledgement.");
                if(dataOffset==outgoingData.Length)throw new InvalidDataException("Missing campaign application receipt.");
                int n=Math.Min(MpCheckpoint.ChunkBytes,outgoingData.Length-dataOffset);var bytes=new byte[n];Buffer.BlockCopy(outgoingData,dataOffset,bytes,0,n);
                Send(new MpMessage{kind="data-chunk",session=session,sequence=dataSerial,unit=dataOffset,blob=bytes});dataOffset+=n;return true;
            }
            if(value.kind=="data-offer") {
                if(host || incomingData!=null || value.sequence<=incomingDataSerial || value.unit<1 || value.unit>MpStateCodec.MaxBytes)throw new InvalidDataException("Invalid campaign data offer.");
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
                    var bytes=incomingData.ToArray();if(MpCheckpoint.Hash(bytes)!=dataHash)throw new InvalidDataException("Campaign data checksum mismatch.");
                    var changes=MpStateCodec.Decode(DataSchemas,bytes);
                    var rows=Baseline.Prepare(incomingDataSerial,incomingBaseRevision,changes,incomingStateHash);
                    ApplyLifecycleData(rows);
                    ApplyDispatches(rows);
                    VerifyAppliedScalars(rows);
                    Baseline.Commit(incomingDataSerial,rows,incomingStateHash);
                    incomingData.Dispose();incomingData=null;
                    if(incomingDataSerial==1)LogDiagnostic("Campaign data applied: "+rows.Length+" records, "+bytes.Length+" compressed bytes; schema="+MpStateCodec.SchemaHash(DataSchemas));
                    Send(new MpMessage{kind="data-applied",session=session,sequence=incomingDataSerial,stateHash=Baseline.Hash});
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
