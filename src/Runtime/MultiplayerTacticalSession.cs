using System;
using System.IO;

namespace GTIP
{
    public sealed class MpTacticalLoadRequest
    {
        public readonly int Generation;
        public readonly bool ReturningToCampaign;
        public readonly string Hash;
        public readonly byte[] Checkpoint;
        internal MpTacticalLoadRequest(int generation,bool returning,string hash,byte[] checkpoint)
        {Generation=generation;ReturningToCampaign=returning;Hash=hash;Checkpoint=checkpoint;}
    }

    // Unity-independent session controller. Archive receipt is distinct from native
    // scene readiness: only SceneLoaded can attest the initialized world's digest.
    // No running-combat phase or guest result calculation is enabled by this layer.
    public sealed class MpTacticalSession : IDisposable
    {
        readonly bool host;
        readonly Action<MpMessage> send;
        readonly Action<MpTacticalLoadRequest> load;
        readonly MpTacticalLifecycle lifecycle;
        byte[] outgoing,verified;
        MemoryStream incoming;
        string transferHash;
        int transferStage,transferLength,sentEnd,lastStart=-1,lastEnd=-1;
        bool received;
        MpTacticalLoadRequest activeLoad;
        public MpTacticalLifecycle Lifecycle => lifecycle;
        public bool TransferComplete => received;
        public int BufferedBytes => incoming==null?0:(int)incoming.Length;
        public int PendingBytes => outgoing==null?0:outgoing.Length;

        public MpTacticalSession(bool host,string session,Action<MpMessage> send,Action<MpTacticalLoadRequest> load)
        {
            this.host=host;this.send=send??throw new ArgumentNullException(nameof(send));
            this.load=load??throw new ArgumentNullException(nameof(load));
            lifecycle=new MpTacticalLifecycle(host);lifecycle.Reset(session);
        }
        public void BeginBattle(byte[] checkpoint,double now)
        {
            RequireHost();var bytes=CopyCheckpoint(checkpoint);string hash=MpCheckpoint.Hash(bytes);
            lifecycle.PrepareBattle(checked(lifecycle.Generation+1),hash,now);
            StartSending(bytes,hash,1,now);
        }
        public void BeginReturn(byte[] campaignCheckpoint,double now)
        {
            RequireHost();var bytes=CopyCheckpoint(campaignCheckpoint);string hash=MpCheckpoint.Hash(bytes);
            if(outgoing!=null)throw new InvalidOperationException("Battle checkpoint receipt is still pending.");
            lifecycle.BeginReturn(hash,now);StartSending(bytes,hash,2,now);
        }
        void StartSending(byte[] bytes,string hash,int stage,double now)
        {
            ClearTransfer();outgoing=bytes;transferHash=hash;transferStage=stage;transferLength=bytes.Length;
            send(Frame("tactical-offer",bytes.Length,hash));
            if(stage==1)lifecycle.BeginLoading(lifecycle.Session,lifecycle.Generation,now);
            RequestLoad(bytes,now);
        }
        static byte[] CopyCheckpoint(byte[] value)
        {
            if(value==null || value.Length==0 || value.Length>MpCheckpoint.MaxBytes)throw new InvalidDataException("Invalid tactical checkpoint size.");
            return (byte[])value.Clone();
        }
        MpMessage Frame(string kind,int offset=0,string text=null,byte[] bytes=null) => new MpMessage {
            kind=kind,session=lifecycle.Session,sequence=lifecycle.Generation,baseRevision=transferStage,
            mode=transferStage==1?"battle":"campaign",unit=offset,text=text,blob=bytes
        };
        public static bool IsTactical(MpMessage value) => value!=null && value.kind!=null && value.kind.StartsWith("tactical-",StringComparison.Ordinal);
        public bool Receive(MpMessage value,double now)
        {
            try {return ReceiveFrame(value,now);}
            finally {if(lifecycle.Phase==MpTacticalPhase.Faulted)ClearTransfer();}
        }
        bool ReceiveFrame(MpMessage value,double now)
        {
            if(!IsTactical(value))return false;
            ValidateEnvelope(value);
            if(value.kind=="tactical-offer") {ReceiveOffer(value,now);return true;}
            lifecycle.ValidateContext(value.session,value.sequence);
            if(value.baseRevision!=transferStage || value.mode!=(transferStage==1?"battle":"campaign"))
                throw new InvalidDataException("Stale tactical transfer stage.");
            switch(value.kind) {
                case "tactical-next": SendChunk(value,now);break;
                case "tactical-chunk": ReceiveChunk(value,now);break;
                case "tactical-received": ReceiveReceipt(value,now);break;
                case "tactical-ready":
                    if(value.unit!=0 || HasBytes(value))throw new InvalidDataException("Invalid native readiness receipt.");
                    if(host && !received)throw new InvalidDataException("Native readiness precedes verified checkpoint receipt.");
                    if(transferStage==1)lifecycle.ReceiveBattleReady(value.session,value.sequence,value.text,now);
                    else lifecycle.ReceiveCampaignReady(value.session,value.sequence,value.text,now);
                    break;
                default: throw new InvalidDataException("Unknown tactical message.");
            }
            return true;
        }
        static bool HasBytes(MpMessage v) => v.blob!=null && v.blob.Length!=0;
        static void ValidateEnvelope(MpMessage v)
        {
            if(v.protocol!=MpRules.Protocol || string.IsNullOrEmpty(v.session) || v.session.Length>128 || v.sequence<=0 ||
                (v.baseRevision!=1 && v.baseRevision!=2) || v.mode!=(v.baseRevision==1?"battle":"campaign") ||
                !string.IsNullOrEmpty(v.token) || !string.IsNullOrEmpty(v.campaign) || !string.IsNullOrEmpty(v.stateHash) ||
                v.clock!=null || (v.units!=null && v.units.Length!=0) || (v.towns!=null && v.towns.Length!=0) ||
                v.worldGeneration!=0 || v.side!=0 || v.x!=0 || v.z!=0 || v.date!=0 || v.paused)
                throw new InvalidDataException("Invalid tactical envelope.");
        }
        void ReceiveOffer(MpMessage v,double now)
        {
            if(host || v.unit<1 || v.unit>MpCheckpoint.MaxBytes || HasBytes(v))throw new InvalidDataException("Invalid tactical checkpoint offer.");
            // Validate before replacing any buffered transfer. Duplicate offers may
            // repeat the existing pull request but cannot rewind a scene or timer.
            bool fresh=v.baseRevision==1?lifecycle.ReceiveBattleOffer(v.session,v.sequence,v.text,now):
                lifecycle.ReceiveReturnOffer(v.session,v.sequence,v.text,now);
            if(!fresh) {
                if(v.baseRevision!=transferStage || v.unit!=transferLength || v.text!=transferHash)
                    throw new InvalidDataException("Conflicting tactical checkpoint offer.");
                send(Frame(received?"tactical-received":"tactical-next",received?transferLength:(int)incoming.Length,received?transferHash:null));return;
            }
            ClearTransfer();transferStage=v.baseRevision;transferLength=v.unit;transferHash=v.text;incoming=new MemoryStream();
            send(Frame("tactical-next"));
        }
        void SendChunk(MpMessage v,double now)
        {
            if(!host || outgoing==null || HasBytes(v) || !string.IsNullOrEmpty(v.text))throw new InvalidDataException("Unexpected tactical checkpoint request.");
            bool duplicate=lastStart>=0 && v.unit==lastStart;
            if(!duplicate && v.unit!=sentEnd)throw new InvalidDataException("Out-of-order tactical checkpoint request.");
            if(v.unit<0 || v.unit>=outgoing.Length)throw new InvalidDataException("Invalid tactical checkpoint offset.");
            int count=Math.Min(MpCheckpoint.ChunkBytes,outgoing.Length-v.unit);var bytes=new byte[count];
            Buffer.BlockCopy(outgoing,v.unit,bytes,0,count);
            if(!duplicate) {lastStart=v.unit;lastEnd=v.unit+count;sentEnd=lastEnd;lifecycle.Progress(v.session,v.sequence,now);}
            send(Frame("tactical-chunk",v.unit,null,bytes));
        }
        void ReceiveChunk(MpMessage v,double now)
        {
            if(host || !HasBytes(v) || v.blob.Length>MpCheckpoint.ChunkBytes || !string.IsNullOrEmpty(v.text))throw new InvalidDataException("Invalid tactical checkpoint chunk.");
            int offset=received?transferLength:incoming==null?-1:(int)incoming.Length;
            if(v.unit<0 || (long)v.unit+v.blob.Length>transferLength)throw new InvalidDataException("Tactical chunk exceeds checkpoint bounds.");
            if(v.unit<offset) {
                // Exact retry of the most recent chunk is harmless. A changed retry
                // cannot overwrite verified bytes or allocate another native scene.
                if(v.unit!=lastStart || v.unit+v.blob.Length!=lastEnd)throw new InvalidDataException("Stale tactical checkpoint chunk.");
                byte[] prior=received?verified:incoming.GetBuffer();
                for(int i=0;i<v.blob.Length;i++)if(prior[v.unit+i]!=v.blob[i])throw new InvalidDataException("Conflicting tactical checkpoint retry.");
                send(Frame(received?"tactical-received":"tactical-next",offset,received?transferHash:null));return;
            }
            if(received || incoming==null || v.unit!=offset)throw new InvalidDataException("Out-of-order tactical checkpoint chunk.");
            incoming.Write(v.blob,0,v.blob.Length);lastStart=v.unit;lastEnd=v.unit+v.blob.Length;
            lifecycle.Progress(v.session,v.sequence,now);
            if(incoming.Length==transferLength) {
                var bytes=incoming.ToArray();incoming.Dispose();incoming=null;
                if(MpCheckpoint.Hash(bytes)!=transferHash){Fail("Tactical checkpoint checksum mismatch.");throw new InvalidDataException(lifecycle.Failure);}
                verified=bytes;received=true;send(Frame("tactical-received",transferLength,transferHash));
                if(transferStage==1)lifecycle.BeginLoading(v.session,v.sequence,now);
                RequestLoad(bytes,now);
            } else send(Frame("tactical-next",(int)incoming.Length));
        }
        void ReceiveReceipt(MpMessage v,double now)
        {
            if(!host || v.unit!=transferLength || v.text!=transferHash || HasBytes(v))throw new InvalidDataException("Invalid tactical checkpoint receipt.");
            if(received)return;
            if(outgoing==null || sentEnd!=outgoing.Length)throw new InvalidDataException("Premature tactical checkpoint receipt.");
            received=true;outgoing=null;
            // Native readiness may already have completed the paused barrier.
            if(lifecycle.Phase!=MpTacticalPhase.PausedBattle && lifecycle.Phase!=MpTacticalPhase.Campaign)
                lifecycle.Progress(v.session,v.sequence,now);
        }
        void RequestLoad(byte[] bytes,double now)
        {
            activeLoad=new MpTacticalLoadRequest(lifecycle.Generation,transferStage==2,transferHash,(byte[])bytes.Clone());
            try {load(activeLoad);}
            catch {Fail("Native tactical checkpoint loading failed.");throw;}
        }
        public void SceneLoaded(MpTacticalLoadRequest request,string baselineHash,double now)
        {
            if(request==null || !ReferenceEquals(request,activeLoad) || request.Generation!=lifecycle.Generation ||
                request.ReturningToCampaign!=(transferStage==2) || request.Hash!=transferHash)
                throw new InvalidDataException("Stale native tactical load completion.");
            try {
                bool fresh=transferStage==1?lifecycle.BattleLoaded(lifecycle.Session,lifecycle.Generation,baselineHash,now):
                    lifecycle.CampaignLoaded(lifecycle.Session,lifecycle.Generation,baselineHash,now);
                if(fresh)send(Frame("tactical-ready",0,baselineHash));
            } finally {if(lifecycle.Phase==MpTacticalPhase.Faulted)ClearTransfer();}
        }
        public bool Tick(double now,double timeoutSeconds)
        {bool failed=lifecycle.Tick(now,timeoutSeconds);if(failed)ClearTransfer();return failed;}
        public void PeerLost(string reason){lifecycle.PeerLost(reason);ClearTransfer();}
        void Fail(string reason){lifecycle.Fault(reason);ClearTransfer();}
        void RequireHost(){if(!host)throw new InvalidOperationException("Only the host can supply tactical checkpoints.");}
        void ClearTransfer()
        {incoming?.Dispose();incoming=null;outgoing=verified=null;activeLoad=null;transferHash=null;transferLength=sentEnd=0;lastStart=lastEnd=-1;received=false;}
        public void Dispose(){PeerLost("Tactical session closed.");}
    }
}
