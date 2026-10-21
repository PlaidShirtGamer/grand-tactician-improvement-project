using System;
using System.Collections.Generic;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        struct CommanderPortraitIdentity
        {
            public Regiment Unit;
            public int Commander;
            public string File;
            public bool Dummy;
            public bool Matches(Regiment unit,int commander,string file,bool dummy) =>
                ReferenceEquals(Unit,unit) && Commander==commander && File==file && Dummy==dummy;
        }
        readonly Dictionary<int,CommanderPortraitIdentity> commanderPortraits=new Dictionary<int,CommanderPortraitIdentity>();

        void ClearCommanderPortraits() => commanderPortraits.Clear();

        void RefreshReplicaCommanderPortraits()
        {
            foreach(var pair in units) {
                var unit=pair.Value;
                if(unit==null || unit.commander<0 || unit.commander>=GameVars.commander.Count || GameVars.commander[unit.commander]==null) {
                    commanderPortraits.Remove(pair.Key);continue;
                }
                var officer=GameVars.commander[unit.commander];
                if(commanderPortraits.TryGetValue(pair.Key,out var prior) && prior.Matches(unit,unit.commander,officer.portraitfilename,officer.usedummyphoto))continue;
                try {
                    // Native AssignCommando performs this display-cache update on the host.
                    // Replication assigns the commander scalar directly; the panel then
                    // reads the new name but the old Regiment.commanderpic unless refreshed.
                    // Keep the native custom-file / historical / placeholder fallback rules.
                    unit.UpdateCommanderPicture();
                    commanderPortraits[pair.Key]=new CommanderPortraitIdentity {
                        Unit=unit,Commander=unit.commander,File=officer.portraitfilename,Dummy=officer.usedummyphoto
                    };
                    officerViewDirty=true;
                }
                catch(Exception error) {
                    // A local image-loading failure must not turn an accepted authoritative
                    // commander assignment into a simulation failure. Retry next data batch.
                    LogDiagnostic("Replica commander portrait refresh failed: formation="+pair.Key+", commander="+unit.commander+"; "+error);
                }
            }
            // IDs can be removed and new ones added in the same lifecycle revision.
            // Prune after insertion so equal-sized replacement rosters are covered too.
            List<int> removed=null;
            foreach(var cached in commanderPortraits)
                if(!units.TryGetValue(cached.Key,out var unit) || !ReferenceEquals(unit,cached.Value.Unit)) {
                    if(removed==null)removed=new List<int>();
                    removed.Add(cached.Key);
                }
            if(removed!=null)foreach(int id in removed)commanderPortraits.Remove(id);
        }
    }
}
