using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GTIP
{
    public sealed class MpFormationState
    {
        public int Parent, Side, Tier, Commander, Garrison, ParentRegiment,Camp,Entrenchment,Fort,SupplyIip,Telegraph;
        public bool Active, Visible, Horse, Top, Detached;
        public float X,Y,Z,Yaw;
        public float[] Colors, Contract;
    }
    public sealed class MpOfficerDates
    {
        public int Promotions;
        public float[] Dates;
    }
    public sealed class MpArmyGroupState
    {
        public int Commander, Override;
        public int[] Members;
        public float[] Color;
    }
    // Local registration is not evidence that the guest has created a formation.
    // Advance this roster only after the corresponding data application receipt.
    public sealed class MpSnapshotRoster
    {
        Dictionary<int,MpUnit> acknowledged=new Dictionary<int,MpUnit>();
        public int Revision {get;private set;}
        public void Acknowledge(int revision,IEnumerable<MpUnit> formations)
        {
            if(revision!=checked(Revision+1))throw new InvalidDataException("Snapshot roster acknowledgement is out of order.");
            var next=new Dictionary<int,MpUnit>();
            foreach(var f in formations) {
                if(f==null || f.id<=0 || f.parent<0 || f.side<0 || f.side>5 || f.tier<0 || f.tier>18 || next.ContainsKey(f.id) || next.Count>=20000)
                    throw new InvalidDataException("Invalid acknowledged snapshot roster.");
                next.Add(f.id,new MpUnit{id=f.id,parent=f.parent,side=f.side,tier=f.tier});
            }
            foreach(var f in next.Values)if(f.parent!=0 && !next.ContainsKey(f.parent))throw new InvalidDataException("Unknown acknowledged snapshot parent.");
            acknowledged=next;Revision=revision;
        }
        public bool Matches(IEnumerable<MpUnit> formations)
        {
            if(Revision==0)return false;
            var seen=new HashSet<int>();
            foreach(var f in formations)
                if(f==null || !seen.Add(f.id) || !acknowledged.TryGetValue(f.id,out var prior) ||
                    f.parent!=prior.parent || f.side!=prior.side || f.tier!=prior.tier)return false;
            return seen.Count==acknowledged.Count;
        }
        public void Clear(){acknowledged.Clear();Revision=0;}
    }
    public static class MpLifecycle
    {
        public static void Validate(IDictionary<int,MpFormationState> formations,int officerCount,int buildings)
        {
            if(formations.Count>20000 || officerCount<0 || officerCount>20000)throw new InvalidDataException("Lifecycle roster limit exceeded.");
            foreach(var pair in formations) {
                var s=pair.Value;
                if(pair.Key<=0 || s==null || s.Side<0 || s.Side>5 || s.Tier<0 || s.Tier>18 || s.Commander<0 || s.Commander>=officerCount ||
                    s.Garrison < -1 || s.Garrison>=buildings || s.Colors==null || s.Colors.Length!=8 || s.Contract==null || s.Contract.Length!=6 ||
                    (s.ParentRegiment!=0 && !formations.ContainsKey(s.ParentRegiment)))throw new InvalidDataException("Invalid formation creation record: "+pair.Key);
                if(!MpRules.Finite(s.X)||!MpRules.Finite(s.Y)||!MpRules.Finite(s.Z)||!MpRules.Finite(s.Yaw))throw new InvalidDataException("Invalid lifecycle position.");
                var seen=new HashSet<int>();int id=pair.Key;
                while(id!=0) {
                    if(!seen.Add(id) || seen.Count>128 || !formations.TryGetValue(id,out var parent))throw new InvalidDataException("Invalid lifecycle parent graph.");
                    id=parent.Parent;
                }
            }
        }
    }
}
