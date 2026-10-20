using System;
using System.Collections.Generic;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        void ApplyReplica(MpMessage value)
        {
            if(!hasLiveData)throw new InvalidOperationException("Map view precedes authoritative campaign data.");
            if(value.campaign!=manifest)throw new InvalidOperationException("Host campaign manifest changed; reload the same checkpoint on both clients.");
            var seen=new HashSet<int>();
            foreach(var state in value.units) {
                if(!seen.Add(state.id) || !units.TryGetValue(state.id,out var unit) || unit==null || unit.alliance!=state.side || unit.unittyp!=state.tier)
                    throw new InvalidOperationException("Snapshot formation does not match the loaded checkpoint: "+state.id);
            }
            // This packet is captured after a campaign-data receipt and its sequence
            // identifies the acknowledged roster, not the time of its coordinates.
            // Applying those newer coordinates would overwrite the verified data
            // baseline. A following scalar-only delta has no formation pose to undo
            // that independent write. Campaign data is the sole logical pose owner;
            // timestamped motion frames continue to drive local visual interpolation.
            foreach(var pair in units) {
                if(pair.Value!=null) {
                    replicaVisibility[pair.Value]=seen.Contains(pair.Key);
                    // Preserve native activation separately from map visibility. An
                    // invisible parent can carry a visible subordinate formation.
                    pair.Value.gameObject.SetActive(originalActive[pair.Value]);
                }
            }
            // The caller retains the complete snapshot for controllability, movement
            // targets and other current order/UI metadata; only logical pose writes
            // are excluded here.
            message="Native map synchronized | "+value.units.Length+" formations | host "+(value.paused?"paused":"running");
        }
    }
}
