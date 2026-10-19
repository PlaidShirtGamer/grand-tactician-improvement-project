using GTIP;

static class SnapshotRosterChecks
{
    internal static int Run()
    {
        int checks=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        void Reject(Action action,string name)
        {
            bool rejected=false;
            try{action();}catch(InvalidDataException){rejected=true;}catch(InvalidOperationException){rejected=true;}catch(ArgumentException){rejected=true;}
            Check(rejected,name);
        }
        MpUnit Unit(int id,int parent=0,int side=0,int tier=0)=>new MpUnit{id=id,parent=parent,side=side,tier=tier};
        MpUnit Clone(MpUnit u)=>Unit(u.id,u.parent,u.side,u.tier);
        List<MpUnit> Copy(IEnumerable<MpUnit> source)=>source.Select(Clone).ToList();

        var acknowledged=new MpSnapshotRoster();
        var live=new List<MpUnit>{Unit(1937,0,0,16),Unit(1938,1937,0,14),Unit(1939,0,1,14)};
        Check(acknowledged.Revision==0 && !acknowledged.Matches(live),"A registry populated from the checkpoint is not an acknowledged live generation");
        Check(!acknowledged.Matches(Array.Empty<MpUnit>()),"An empty roster cannot enable snapshots before the first acknowledgement");
        acknowledged.Acknowledge(1,live);
        Check(acknowledged.Revision==1 && acknowledged.Matches(live),"The first applied lifecycle permits a matching full-roster snapshot");
        Check(acknowledged.Matches(live.AsEnumerable().Reverse()),"Roster enumeration order has no bearing on snapshot eligibility");

        // The host has already prepared revision 2 when a native recruitment
        // registers formation 1940 in its local IDs. The old receipt must not
        // make the enlarged local registry eligible for a revision 2 snapshot.
        var beforeRecruit=Copy(live);
        live.Add(Unit(1940,1938));
        Check(!acknowledged.Matches(live),"Recruitment is blocked even after the new formation is registered locally");
        acknowledged.Acknowledge(2,beforeRecruit);
        Check(acknowledged.Revision==2 && !acknowledged.Matches(live),"A pre-recruitment revision receipt cannot publish the new formation under the old roster");
        acknowledged.Acknowledge(3,live);
        Check(acknowledged.Matches(live),"The recruited unit becomes eligible only after its lifecycle revision is acknowledged");

        var inFlight=Copy(live);
        live.Add(Unit(1941,1938));
        Check(!acknowledged.Matches(live),"A second native mutation blocks snapshots while another revision is in flight");
        acknowledged.Acknowledge(4,inFlight);
        Check(!acknowledged.Matches(live),"Acknowledging an in-flight roster does not acknowledge a later recruitment");
        acknowledged.Acknowledge(5,live);
        Check(acknowledged.Matches(live),"Acknowledging the second recruitment restores snapshot eligibility");

        Check(!acknowledged.Matches(live.Where(u=>u.side==0)),"Snapshot eligibility uses the full roster, including currently hidden opponents");
        Check(!acknowledged.Matches(live.Where(u=>u.id!=1940)),"Removal is blocked until the lifecycle deletion is acknowledged");
        var changed=Copy(live);changed.Single(u=>u.id==1940).tier=1;
        Check(!acknowledged.Matches(changed),"Changing a formation tier requires a matching acknowledged lifecycle");
        changed=Copy(live);changed.Single(u=>u.id==1940).side=1;
        Check(!acknowledged.Matches(changed),"Changing a formation side requires a matching acknowledged lifecycle");
        changed=Copy(live);changed.Single(u=>u.id==1940).parent=1937;
        Check(!acknowledged.Matches(changed),"Reparenting requires a matching acknowledged lifecycle");
        changed=Copy(live);changed.Single(u=>u.id==1940).id=1942;
        Check(!acknowledged.Matches(changed),"An equal-sized roster with a replacement formation remains blocked");
        changed=Copy(live);changed[changed.Count-1]=Clone(changed[0]);
        Check(!acknowledged.Matches(changed),"A duplicate ID cannot hide a missing formation in an equal-sized roster");

        changed=Copy(live);
        foreach(var u in changed){u.name="renamed";u.commander="new commander";u.men=678;u.x=99;u.y=2;u.z=17;u.yaw=45;u.moving=true;u.controllable=true;}
        Check(acknowledged.Matches(changed),"Pose, strength and other scalar presentation changes do not stall snapshots");

        var immutable=new MpSnapshotRoster();var source=Copy(live);
        immutable.Acknowledge(1,source);
        source.Single(u=>u.id==1940).parent=1937;
        source.RemoveAt(0);
        Check(immutable.Matches(live),"Acknowledgement owns copies of roster identities instead of mutable source objects");
        Check(!immutable.Matches(source),"Mutating an acknowledged input object cannot silently change the acknowledged roster");
        Reject(()=>acknowledged.Acknowledge(5,live),"A duplicate acknowledgement cannot advance or replace the generation");
        Reject(()=>acknowledged.Acknowledge(4,live),"A stale acknowledgement cannot roll the roster backwards");
        Reject(()=>acknowledged.Acknowledge(7,live),"A skipped generation cannot be acknowledged");
        Check(acknowledged.Revision==5 && acknowledged.Matches(live),"Rejected acknowledgements preserve the last valid roster and revision");

        var removed=live.Where(u=>u.id!=1940).ToList();acknowledged.Acknowledge(6,removed);
        Check(acknowledged.Matches(removed) && !acknowledged.Matches(live),"An acknowledged deletion enables exactly the surviving roster");
        acknowledged.Clear();
        Check(acknowledged.Revision==0 && !acknowledged.Matches(removed),"Disconnect clears acknowledged state and blocks reused local IDs");
        Reject(()=>acknowledged.Acknowledge(6,removed),"A previous session's generation cannot restore snapshot permission");
        acknowledged.Acknowledge(1,removed);
        Check(acknowledged.Revision==1 && acknowledged.Matches(removed),"A new session can acknowledge reused IDs from its own first generation");
        Console.WriteLine("Snapshot roster regression: native recruitment, repeated mutations, removal and reparenting wait for the exact acknowledged lifecycle; scalar movement remains eligible. No Unity gameplay exercised.");
        return checks;
    }
}
