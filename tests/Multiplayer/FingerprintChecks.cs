using GTIP;
using System.Diagnostics;

static class FingerprintChecks
{
    sealed class Fixture {public float Number;public float[] Values;public int[] Counts;public float[,] Matrix;public string Name;}
    internal static int Run()
    {
        int checks=0;
        var schemas=new[]{MpStateSchema.Scalars(typeof(Fixture),"Number","Values","Matrix")};
        var state=new MpStateBaseline(schemas);
        var f=new Fixture{Number=0,Values=new[]{1f,float.NaN,-0f},Counts=new[]{1,2},Matrix=new[,]{{1f,-0f}},Name="fleet"};
        MpStateRow Row(int id)=>new MpStateRow{Id=id,Values=schemas[0].Capture(f)};
        string Original(MpStateRow[] rows)=>MpCheckpoint.Hash(MpStateCodec.Encode(schemas,rows.OrderBy(r=>((long)r.Kind<<32)|(uint)r.Id).ToArray(),false));
        void Equal(MpStateRow[] rows,string name){if(state.Fingerprint(rows)!=Original(rows))throw new Exception(name);checks++;}
        var rows=new[]{Row(2),Row(1)};Equal(rows,"Initial fingerprint must retain existing wire bytes");Equal(rows,"Cached fingerprint must retain existing wire bytes");
        f.Number=-0f;rows=new[]{Row(1),Row(2)};Equal(rows,"Signed scalar zero cannot reuse the opposite encoding");
        f.Values[2]=0f;f.Matrix[0,1]=0f;rows=new[]{Row(1),Row(2)};Equal(rows,"Signed array zero cannot reuse the opposite encoding");
        f.Counts[0]=3;f.Name="different";rows=new[]{Row(1),Row(2)};Equal(rows,"In-place source edits must invalidate cached content");
        Equal(new[]{Row(2)},"Deleted rows must not remain in cached fingerprints");
        Equal(new[]{Row(2),Row(3)},"Inserted rows and subsequent cached rows agree with full encoding");
        var baseline=state.Prepare(1,0,new[]{Row(1),Row(2)});var hash=state.Fingerprint(baseline);state.Commit(1,baseline,hash);checks++;
        f.Counts[0]=4;var changes=state.Changes(new[]{Row(1),Row(2)});var next=state.Prepare(2,1,changes);var nextHash=state.Fingerprint(next);state.Commit(2,next,nextHash);checks++;
        if(nextHash!=Original(next))throw new Exception("Committed incremental checksum differs from full encoding");checks++;
        f.Number=123;var altered=state.Prepare(3,2,new[]{Row(1)});bool rejected=false;
        try{state.Commit(3,altered,nextHash);}catch(InvalidDataException){rejected=true;}if(!rejected || state.Revision!=2)throw new Exception("Cached checksums must still reject an altered commit");checks++;
        // Repeat the observed roster scale without Unity. This is a synthetic throughput
        // measurement, not a bound on native capture, network latency or game performance.
        f.Values=Enumerable.Range(0,48).Select(i=>(float)i).ToArray();f.Number=1;
        var large=Enumerable.Range(0,18000).Select(Row).ToArray();state.Fingerprint(large);
        var timer=Stopwatch.StartNew();var reference=Original(large);timer.Stop();double full=timer.Elapsed.TotalMilliseconds;
        timer.Restart();var cached=state.Fingerprint(large);timer.Stop();double warm=timer.Elapsed.TotalMilliseconds;
        if(cached!=reference)throw new Exception("Large cached snapshot differs from full encoding");checks++;
        Console.WriteLine($"Synthetic 18,000-row fingerprint: full encoding {full:F1} ms; warm cache {warm:F1} ms; identical SHA-256. No Unity or network timing measured.");
        return checks;
    }
}
