using System;
using System.IO;

namespace GTIP
{
    // A reliable FIFO barrier, separate from scene readiness. The host cannot
    // destroy native campaign objects while a captured revision is still in flight.
    public sealed class MpTacticalDrain
    {
        readonly string session;
        readonly int generation;
        int peerRevision;
        string peerHash;
        public bool PeerHeld {get;private set;}
        public bool Committed {get;private set;}
        public MpTacticalDrain(string session,int generation)
        {
            if(string.IsNullOrEmpty(session) || session.Length>128 || generation<=0)throw new InvalidDataException("Invalid tactical drain context.");
            this.session=session;this.generation=generation;
        }
        public MpMessage Offer() => new MpMessage{kind="tactical-hold",session=session,sequence=generation};
        public static void ValidateOffer(MpMessage value,string session,int generation)
        {Validate(value,"tactical-hold",session,generation);if(value.baseRevision!=0 || !string.IsNullOrEmpty(value.stateHash))throw new InvalidDataException("Invalid tactical hold offer.");}
        public static MpMessage Receipt(string session,int generation,int revision,string hash)
        {
            Digest(hash);if(revision<=0)throw new InvalidDataException("An applied campaign revision is required.");
            return new MpMessage{kind="tactical-held",session=session,sequence=generation,baseRevision=revision,stateHash=hash};
        }
        public static MpMessage Fence(string session,int generation,int revision,string hash)
        {
            var value=Receipt(session,generation,revision,hash);value.kind="tactical-drained";return value;
        }
        public static void ValidateFence(MpMessage value,string session,int generation,int localRevision,string localHash)
        {
            Validate(value,"tactical-drained",session,generation);Digest(value.stateHash);Digest(localHash);
            if(value.baseRevision<=0 || localRevision<=0 || value.baseRevision!=localRevision || value.stateHash!=localHash)
                throw new InvalidDataException("Campaign revisions disagree at the tactical drain fence.");
        }
        public bool Receive(MpMessage value)
        {
            Validate(value,"tactical-held",session,generation);Digest(value.stateHash);
            if(value.baseRevision<=0)throw new InvalidDataException("Invalid held campaign revision.");
            if(PeerHeld) {
                if(peerRevision!=value.baseRevision || peerHash!=value.stateHash)throw new InvalidDataException("Conflicting tactical drain receipt.");
                return false;
            }
            peerRevision=value.baseRevision;peerHash=value.stateHash;PeerHeld=true;return true;
        }
        public bool Commit(bool busy,int revision,string hash)
        {
            if(Committed || busy || !PeerHeld)return false;
            Digest(hash);
            if(revision!=peerRevision || hash!=peerHash)throw new InvalidDataException("Campaign revisions disagree at tactical handoff.");
            Committed=true;return true;
        }
        static void Digest(string hash)
        {
            if(hash!=null && hash.Length==44)try{var bytes=Convert.FromBase64String(hash);if(bytes.Length==32 && Convert.ToBase64String(bytes)==hash)return;}catch(FormatException){}
            throw new InvalidDataException("Invalid campaign handoff digest.");
        }
        static void Validate(MpMessage v,string kind,string session,int generation)
        {
            if(v==null || v.kind!=kind || v.protocol!=MpRules.Protocol || v.session!=session || v.sequence!=generation ||
                v.worldGeneration!=0 || !string.IsNullOrEmpty(v.token) || !string.IsNullOrEmpty(v.mode) || !string.IsNullOrEmpty(v.text) ||
                !string.IsNullOrEmpty(v.campaign) || v.unit!=0 || v.clock!=null || (v.blob!=null && v.blob.Length!=0) ||
                (v.units!=null && v.units.Length!=0) || (v.towns!=null && v.towns.Length!=0) || v.side!=0 || v.x!=0 || v.z!=0 || v.date!=0 || v.paused)
                throw new InvalidDataException("Invalid tactical handoff control.");
        }
    }
}
