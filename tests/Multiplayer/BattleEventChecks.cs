using GTIP;

static class BattleEventChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;}
        void Reject(Action action,string name){try{action();}catch(System.IO.InvalidDataException){checks++;return;}throw new Exception(name);}
        var cursor=new MpBattleEventCursor();
        Check(cursor.Take(1,new[]{1,2,3},true).Length==0,"Checkpoint battle effects are historical and must not replay");
        Check(cursor.Take(1,new[]{1,2,3},false).Length==0,"A repeated battle revision cannot replay historical effects");
        Check(cursor.Take(1,new[]{1,2,3,4},false).SequenceEqual(new[]{3}),"One newly received host explosion plays once");
        Check(cursor.Take(1,new[]{2,3,4,5,6},false).SequenceEqual(new[]{3,4}),"A bounded event window retains ordered new battle sounds");
        Check(cursor.Take(2,new[]{7,8},false).SequenceEqual(new[]{0,1}),"A battle created after checkpoint plays its first received events");
        Check(cursor.Take(1,new[]{4,5,6},false).Length==0,"Other battles cannot advance this battle's event cursor");
        Check(cursor.Take(2,Array.Empty<int>(),false).Length==0,"An empty battle event window is valid");
        Check(cursor.Take(2,new[]{9},false).SequenceEqual(new[]{0}),"A skipped expired event does not stall later presentation");
        Reject(()=>cursor.Take(0,new[]{10},false),"Battle effects require positive identities");
        Reject(()=>cursor.Take(2,null!,false),"Missing event serial arrays reject");
        Reject(()=>cursor.Take(2,new[]{0},false),"Zero event serials reject");
        Reject(()=>cursor.Take(2,new[]{-1},false),"Negative event serials reject");
        Reject(()=>cursor.Take(2,new[]{10,10},false),"Duplicate event serials reject");
        Reject(()=>cursor.Take(2,new[]{11,10},false),"Reordered effect serials reject");
        Reject(()=>cursor.Take(2,Enumerable.Range(10,129).ToArray(),false),"Oversized event windows reject");
        Check(cursor.Take(2,new[]{10},false).SequenceEqual(new[]{0}),"Invalid windows cannot advance a valid cursor");
        cursor.Clear();
        Check(cursor.Take(1,new[]{1,2},false).SequenceEqual(new[]{0,1}),"Session reset clears presentation receipts");
        Check(cursor.Take(3,Enumerable.Range(20,128).ToArray(),false).Length==128,"Full bounded event window is supported");
        Check(cursor.Take(3,new[]{147,148},false).SequenceEqual(new[]{1}),"Bounded rollover preserves exactly-once presentation");
        int nextSource=10;var sourceIds=new MpBattleSourceIds<object>();var raid=new object();var skirmish=new object();var interception=new object();
        sourceIds.Update(new[]{raid,skirmish},()=>++nextSource);
        int raidId=sourceIds.Sources[raid],skirmishId=sourceIds.Sources[skirmish];
        Check(sourceIds.Sources.Count==2 && raidId!=skirmishId && raidId>10 && skirmishId>10,"Minor combat sources share the global battle identity range without collisions");
        sourceIds.Update(new[]{skirmish,raid,raid},()=>++nextSource);
        Check(sourceIds.Sources[raid]==raidId && sourceIds.Sources[skirmish]==skirmishId && sourceIds.Sources.Count==2,"Source reordering and duplicates retain stable combat identities");
        nextSource++; // A full battle monument allocates from the same counter.
        sourceIds.Update(new[]{raid,skirmish,interception},()=>++nextSource);
        Check(sourceIds.Sources[interception]==nextSource && sourceIds.Sources[interception]>Math.Max(raidId,skirmishId)+1,"Monument allocation and minor combat allocation remain disjoint");
        sourceIds.Update(new[]{skirmish,interception},()=>++nextSource);
        Check(!sourceIds.Sources.ContainsKey(raid) && sourceIds.Sources[skirmish]==skirmishId,"Expired raid sources are removed without replacing active skirmishes");
        sourceIds.Update(new[]{raid,skirmish,interception},()=>++nextSource);
        Check(sourceIds.Sources[raid]>raidId,"A later raid instance cannot reuse a stale presentation receipt");
        sourceIds.Update(new object[]{null!,skirmish},()=>++nextSource);
        Check(sourceIds.Sources.Count==1 && sourceIds.Sources[skirmish]==skirmishId,"Removed native source references do not produce phantom sound sources");
        sourceIds.Update(Array.Empty<object>(),()=>++nextSource);
        Check(sourceIds.Sources.Count==0,"An empty host source roster removes all guest audio sources");
        Reject(()=>sourceIds.Update(new[]{raid},()=>0),"Nonpositive audio source identities reject");
        sourceIds.Update(new[]{raid},()=>++nextSource);
        Reject(()=>sourceIds.Update(new[]{raid,skirmish},()=>sourceIds.Sources[raid]),"Audio source allocation cannot collide with an existing source");
        sourceIds.Clear();nextSource=0;sourceIds.Update(new[]{raid},()=>++nextSource);
        Check(sourceIds.Sources.Count==1 && sourceIds.Sources[raid]==1,"Session reset removes source identities before the shared counter restarts");
        Console.WriteLine($"Battle presentation: {checks} checks passed using production event cursors.");
        return checks;
    }
}
