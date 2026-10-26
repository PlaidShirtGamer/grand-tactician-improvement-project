using System;
using System.IO;

namespace GTIP
{
    public enum MpTacticalPhase
    {
        Campaign, PreparingBattle, LoadingBattle, VerifyingBattle,
        PausedBattle, ReturningCampaign, VerifyingCampaign, Faulted
    }

    // This first milestone deliberately has no running-combat phase. A scene may
    // be displayed only after both peers attest the same authoritative baseline.
    public sealed class MpTacticalLifecycle
    {
        readonly bool host;
        string localBaseline, peerBaseline;
        bool localReady, peerReady;
        double observedAt, progressedAt;
        int lastCampaignSequence;

        public MpTacticalLifecycle(bool host)
        {
            this.host=host; Phase=MpTacticalPhase.Faulted; Failure="Session not initialized.";
        }
        public string Session {get;private set;}
        public int Generation {get;private set;}
        public MpTacticalPhase Phase {get;private set;}
        public string Failure {get;private set;}
        public string BattleCheckpointHash {get;private set;}
        public string CampaignCheckpointHash {get;private set;}
        public bool MustPause => Phase!=MpTacticalPhase.Campaign;
        public bool CanUseCampaign => Phase==MpTacticalPhase.Campaign;
        public bool CombatEnabled => false;
        public bool LocalReady => localReady;
        public bool PeerReady => peerReady;

        public void Reset(string session)
        {
            if(string.IsNullOrWhiteSpace(session) || session.Length>128)throw new InvalidDataException("Invalid tactical session identity.");
            Session=session;Generation=0;Phase=MpTacticalPhase.Campaign;Failure=null;
            BattleCheckpointHash=CampaignCheckpointHash=null;lastCampaignSequence=0;
            observedAt=progressedAt=0;ClearBarrier();
        }
        public void ValidateContext(string session,int generation)
        {
            if(Session==null || session!=Session || generation!=Generation || generation<0)
                throw new InvalidDataException("Stale or foreign tactical context.");
            if(Phase==MpTacticalPhase.Faulted)throw new InvalidOperationException("Tactical session is faulted: "+Failure);
        }
        public void PrepareBattle(int generation,string checkpointHash,double now)
        {
            RequireHost(true);BeginBattle(Session,generation,checkpointHash,now);
        }
        public bool ReceiveBattleOffer(string session,int generation,string checkpointHash,double now)
        {
            RequireHost(false);string hash=Digest(checkpointHash);
            if(session==Session && generation==Generation && hash==BattleCheckpointHash && IsEnteringBattle())return false;
            BeginBattle(session,generation,hash,now);return true;
        }
        void BeginBattle(string session,int generation,string checkpointHash,double now)
        {
            string hash=Digest(checkpointHash);
            if(Session==null || session!=Session)throw new InvalidDataException("Foreign tactical session.");
            RequirePhase(MpTacticalPhase.Campaign);
            if(Generation==int.MaxValue || generation!=Generation+1)throw new InvalidDataException("Battle generation must advance exactly once.");
            Observe(now);Generation=generation;BattleCheckpointHash=hash;CampaignCheckpointHash=null;
            ClearBarrier();Phase=MpTacticalPhase.PreparingBattle;progressedAt=now;
        }
        bool IsEnteringBattle() => Phase==MpTacticalPhase.PreparingBattle || Phase==MpTacticalPhase.LoadingBattle ||
            Phase==MpTacticalPhase.VerifyingBattle || Phase==MpTacticalPhase.PausedBattle;
        public void BeginLoading(string session,int generation,double now)
        {
            ValidateContext(session,generation);RequirePhase(MpTacticalPhase.PreparingBattle);Observe(now);
            Phase=MpTacticalPhase.LoadingBattle;progressedAt=now;
        }
        public bool BattleLoaded(string session,int generation,string baselineHash,double now)
        {
            ValidateContext(session,generation);string hash=Digest(baselineHash);
            if(localReady && (Phase==MpTacticalPhase.VerifyingBattle || Phase==MpTacticalPhase.PausedBattle))return LocalReadyAgain(hash);
            RequirePhase(MpTacticalPhase.LoadingBattle);Observe(now);
            localReady=true;localBaseline=hash;Phase=MpTacticalPhase.VerifyingBattle;progressedAt=now;
            CompleteBarrier(MpTacticalPhase.PausedBattle);return true;
        }
        public bool ReceiveBattleReady(string session,int generation,string baselineHash,double now)
        {
            ValidateContext(session,generation);string hash=Digest(baselineHash);
            if(!IsEnteringBattle())throw new InvalidOperationException("Battle readiness outside battle loading.");
            return PeerReadyFor(hash,now,MpTacticalPhase.PausedBattle);
        }
        public void BeginReturn(string checkpointHash,double now)
        {
            RequireHost(true);StartReturn(Session,Generation,checkpointHash,now);
        }
        public bool ReceiveReturnOffer(string session,int generation,string checkpointHash,double now)
        {
            RequireHost(false);ValidateContext(session,generation);string hash=Digest(checkpointHash);
            if(hash==CampaignCheckpointHash && (Phase==MpTacticalPhase.ReturningCampaign || Phase==MpTacticalPhase.VerifyingCampaign || Phase==MpTacticalPhase.Campaign))return false;
            StartReturn(session,generation,hash,now);return true;
        }
        void StartReturn(string session,int generation,string checkpointHash,double now)
        {
            ValidateContext(session,generation);string hash=Digest(checkpointHash);RequirePhase(MpTacticalPhase.PausedBattle);Observe(now);
            CampaignCheckpointHash=hash;ClearBarrier();Phase=MpTacticalPhase.ReturningCampaign;progressedAt=now;
        }
        public bool CampaignLoaded(string session,int generation,string baselineHash,double now)
        {
            ValidateContext(session,generation);string hash=Digest(baselineHash);
            if(localReady && (Phase==MpTacticalPhase.VerifyingCampaign || Phase==MpTacticalPhase.Campaign) && Generation>0)return LocalReadyAgain(hash);
            RequirePhase(MpTacticalPhase.ReturningCampaign);Observe(now);
            localReady=true;localBaseline=hash;Phase=MpTacticalPhase.VerifyingCampaign;progressedAt=now;
            CompleteBarrier(MpTacticalPhase.Campaign);return true;
        }
        public bool ReceiveCampaignReady(string session,int generation,string baselineHash,double now)
        {
            ValidateContext(session,generation);string hash=Digest(baselineHash);
            if(Generation<=0 || CampaignCheckpointHash==null || (Phase!=MpTacticalPhase.ReturningCampaign && Phase!=MpTacticalPhase.VerifyingCampaign && Phase!=MpTacticalPhase.Campaign))
                throw new InvalidOperationException("Campaign readiness outside campaign restoration.");
            return PeerReadyFor(hash,now,MpTacticalPhase.Campaign);
        }
        bool LocalReadyAgain(string hash)
        {
            if(localBaseline!=hash){Fault("Local initialized baseline changed after readiness.");throw new InvalidDataException(Failure);}
            return false;
        }
        bool PeerReadyFor(string hash,double now,MpTacticalPhase destination)
        {
            if(peerReady) {
                if(peerBaseline!=hash){Fault("Peer changed its ready baseline.");throw new InvalidDataException(Failure);}
                return false;
            }
            Observe(now);peerReady=true;peerBaseline=hash;progressedAt=now;CompleteBarrier(destination);return true;
        }
        void CompleteBarrier(MpTacticalPhase destination)
        {
            if(!localReady || !peerReady)return;
            if(localBaseline!=peerBaseline){Fault("Host and guest baseline hashes differ.");throw new InvalidDataException(Failure);}
            Phase=destination;
        }
        public void ClaimCampaignCommand(string session,int generation,int sequence)
        {
            RequireHost(true);ValidateContext(session,generation);RequirePhase(MpTacticalPhase.Campaign);
            if(sequence<=0 || sequence<=lastCampaignSequence)throw new InvalidDataException("Duplicate or stale campaign command.");
            lastCampaignSequence=sequence;
        }
        public void ClaimBattleCommand(string session,int generation,int sequence)
        {
            RequireHost(true);ValidateContext(session,generation);
            throw new InvalidOperationException("Combat orders are unavailable in the paused battle milestone.");
        }
        public void Progress(string session,int generation,double now)
        {
            ValidateContext(session,generation);
            if(!Transitioning())throw new InvalidOperationException("No tactical transition is in progress.");
            Observe(now);progressedAt=now;
        }
        public bool Tick(double now,double timeoutSeconds)
        {
            if(double.IsNaN(timeoutSeconds) || double.IsInfinity(timeoutSeconds) || timeoutSeconds<=0)throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            Observe(now);
            if(!Transitioning() || now-progressedAt<timeoutSeconds)return false;
            Fault("Tactical transition timed out in "+Phase+".");return true;
        }
        public void PeerLost(string reason) { Fault(string.IsNullOrWhiteSpace(reason)?"Peer disconnected during tactical session.":reason); }
        public void Fault(string reason) { Failure=string.IsNullOrWhiteSpace(reason)?"Tactical session failed.":reason;Phase=MpTacticalPhase.Faulted; }
        bool Transitioning() => Phase==MpTacticalPhase.PreparingBattle || Phase==MpTacticalPhase.LoadingBattle || Phase==MpTacticalPhase.VerifyingBattle ||
            Phase==MpTacticalPhase.ReturningCampaign || Phase==MpTacticalPhase.VerifyingCampaign;
        void ClearBarrier(){localReady=peerReady=false;localBaseline=peerBaseline=null;}
        void RequireHost(bool expected){if(host!=expected)throw new InvalidOperationException(expected?"Only the host can initiate this operation.":"A host cannot accept a host offer.");}
        void RequirePhase(MpTacticalPhase expected){if(Phase!=expected)throw new InvalidOperationException("Expected "+expected+", current phase is "+Phase+".");}
        void Observe(double now)
        {
            if(double.IsNaN(now) || double.IsInfinity(now) || now<observedAt || now<0)throw new ArgumentOutOfRangeException(nameof(now),"Use a monotonic local clock.");
            observedAt=now;
        }
        static string Digest(string hash)
        {
            if(hash!=null && hash.Length==44) {
                try {
                    var bytes=Convert.FromBase64String(hash);
                    if(bytes.Length==32 && Convert.ToBase64String(bytes)==hash)return hash;
                } catch(FormatException) { }
            }
            throw new InvalidDataException("Expected a SHA-256 baseline digest.");
        }
    }
}
