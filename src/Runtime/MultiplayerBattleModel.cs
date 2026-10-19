using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GTIP
{
    public sealed class MpBattleRecord
    {
        public string Key;
        public bool Active;
        public float X,Y,Z;
        public int[] From,To,Top0,Top1;
        public int UnitSides,FleetSides;
    }
    public sealed class MpBattleAudioSource
    {
        public int Type;
        public float X,Y,Z;
    }
    public sealed class MpBattleSourceIds<T> where T:class
    {
        readonly Dictionary<T,int> sources=new Dictionary<T,int>();
        public IReadOnlyDictionary<T,int> Sources=>sources;
        public void Update(IEnumerable<T> current,Func<int> allocate)
        {
            var live=new HashSet<T>(current.Where(s=>s!=null));
            foreach(var source in sources.Keys.ToArray())if(!live.Contains(source))sources.Remove(source);
            foreach(var source in live)if(!sources.ContainsKey(source)) {
                int id=allocate();if(id<=0 || sources.ContainsValue(id))throw new InvalidDataException("Invalid battle source identity allocation.");
                sources[source]=id;
            }
        }
        public void Clear(){sources.Clear();}
    }
    public sealed class MpBattleSide
    {
        public int[] Top,Single,Parent,Ammo,TopCasualties,Fleets,Vessels,VesselParents,SunkenTypes;
        public float[] Arrival;
        public bool[] Arrived,Withdrawn;
        public string[] SunkenNames;
        public float SiegeStrength;
        public int Sunken;
    }
    public sealed class MpBattlePresentation
    {
        public string[] Log;
        public int[] LoopPurpose;
        public bool[] LoopActive;
        public int[] StatusSide,StatusKind,StatusUnit,StatusVessel,StatusSunken;
        public float[] StatusTime,StatusDuration;
        public int[] EventSerial,EventKind,EventPurpose;
        public bool[] EventActive;
        public float[] EventX,EventY,EventZ;
    }
    // Presentation events are bounded independently from authoritative battle records.
    // A first checkpoint never replays old explosions; later snapshots consume each
    // host event once, even if unrelated state refreshes the same battle row.
    public sealed class MpBattleEventCursor
    {
        readonly Dictionary<int,int> seen=new Dictionary<int,int>();
        public int[] Take(int battle,int[] serials,bool checkpoint)
        {
            if(battle<=0 || serials==null || serials.Length>128 || serials.Any(s=>s<=0) || serials.Zip(serials.Skip(1),(a,b)=>b>a).Any(v=>!v))throw new InvalidDataException("Invalid battle presentation serials.");
            int prior=seen.TryGetValue(battle,out int last)?last:0;
            if(serials.Length>0)seen[battle]=Math.Max(prior,serials[serials.Length-1]);
            return checkpoint?Array.Empty<int>():Enumerable.Range(0,serials.Length).Where(i=>serials[i]>prior).ToArray();
        }
        public void Clear(){seen.Clear();}
    }
}
