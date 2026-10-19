using GTIP;
using System.Diagnostics;

static class FingerprintChecks
{
    sealed class Fixture
    {
        public float Number;public float[] Values;public int[] Counts;public float[,] Matrix;
        public int[,] CountMatrix;public bool[,] FlagMatrix;public string[,] TextMatrix;public string Name;
    }
    internal static int Run()
    {
        int checks=0;
        var schemas=new[]{MpStateSchema.Scalars(typeof(Fixture),"Number","Values","Matrix")};
        var state=new MpStateBaseline(schemas);
        var f=new Fixture{Number=0,Values=new[]{1f,float.NaN,-0f},Counts=new[]{1,2},Matrix=new[,]{{1f,-0f}},
            CountMatrix=new[,]{{int.MinValue,0},{int.MaxValue,3}},FlagMatrix=new[,]{{false,true},{true,false}},
            TextMatrix=new[,]{{null,""},{"fleet","Dover, DÉ"}},Name="fleet"};
        MpStateRow Row(int id)=>new MpStateRow{Id=id,Values=schemas[0].Capture(f)};
        string Original(MpStateRow[] rows)=>MpCheckpoint.Hash(MpStateCodec.Encode(schemas,rows.OrderBy(r=>((long)r.Kind<<32)|(uint)r.Id).ToArray(),false));
        void Equal(MpStateRow[] rows,string name){if(state.Fingerprint(rows)!=Original(rows))throw new Exception(name);checks++;}
        var rows=new[]{Row(2),Row(1)};Equal(rows,"Initial fingerprint must retain existing wire bytes");Equal(rows,"Cached fingerprint must retain existing wire bytes");
        f.CountMatrix[1,1]=4;rows=new[]{Row(1),Row(2)};Equal(rows,"In-place integer matrix edits invalidate cached bytes");
        f.FlagMatrix[1,1]=true;rows=new[]{Row(1),Row(2)};Equal(rows,"In-place boolean matrix edits invalidate cached bytes");
        f.TextMatrix[1,1]="DOVER, DÉ";rows=new[]{Row(1),Row(2)};Equal(rows,"String matrix equality preserves case-sensitive UTF-8 wire bytes");
        f.TextMatrix[0,0]="";rows=new[]{Row(1),Row(2)};Equal(rows,"String matrices distinguish null from an empty string");
        f.CountMatrix=new[,]{{int.MinValue,0,int.MaxValue,4}};rows=new[]{Row(1),Row(2)};Equal(rows,"Integer matrix shape changes invalidate caches even with identical flattened values");
        f.FlagMatrix=new[,]{{false,true,true,true}};rows=new[]{Row(1),Row(2)};Equal(rows,"Boolean matrix shape changes invalidate caches even with identical flattened values");
        f.TextMatrix=new[,]{{"","","fleet","DOVER, DÉ"}};rows=new[]{Row(1),Row(2)};Equal(rows,"String matrix shape changes invalidate caches even with identical flattened values");
        f.CountMatrix=new int[0,2];f.FlagMatrix=new bool[0,2];f.TextMatrix=new string[0,2];rows=new[]{Row(1),Row(2)};Equal(rows,"Empty matrices retain their encoded dimensions");
        f.CountMatrix=new int[2,0];f.FlagMatrix=new bool[2,0];f.TextMatrix=new string[2,0];rows=new[]{Row(1),Row(2)};Equal(rows,"Different zero-length matrix shapes cannot reuse old dimensions");
        f.CountMatrix=null;f.FlagMatrix=null;f.TextMatrix=null;rows=new[]{Row(1),Row(2)};Equal(rows,"Null typed matrices cannot reuse empty matrix encodings");
        f.CountMatrix=new[,]{{7}};f.FlagMatrix=new[,]{{true}};f.TextMatrix=new[,]{{"restored"}};rows=new[]{Row(1),Row(2)};Equal(rows,"Typed matrices can be restored after null without stale cache entries");
        f.Number=-0f;rows=new[]{Row(1),Row(2)};Equal(rows,"Signed scalar zero cannot reuse the opposite encoding");
        f.Values[2]=0f;rows=new[]{Row(1),Row(2)};Equal(rows,"Signed one-dimensional array zero cannot reuse the opposite encoding");
        f.Matrix[0,1]=0f;rows=new[]{Row(1),Row(2)};Equal(rows,"Signed matrix zero cannot reuse the opposite encoding");
        f.Matrix=new[,]{{1f},{0f}};rows=new[]{Row(1),Row(2)};Equal(rows,"Float matrix shape changes invalidate caches even with identical flattened values");
        f.Matrix=new[,]{{float.NaN,-0f}};f.Number=float.NaN;rows=new[]{Row(1),Row(2)};Equal(rows,"Registered scalar and matrix NaNs retain canonical codec bytes");
        string nanHash=state.Fingerprint(rows);
        f.Matrix[0,0]=BitConverter.Int32BitsToSingle(unchecked((int)0xffa01234));f.Values[1]=BitConverter.Int32BitsToSingle(0x7fc01234);f.Number=BitConverter.Int32BitsToSingle(0x7f801234);
        rows=new[]{Row(1),Row(2)};Equal(rows,"Different native NaN payloads share the codec's canonical NaN encoding");
        if(state.Fingerprint(rows)!=nanHash)throw new Exception("Canonical NaN payloads changed the campaign checksum");checks++;
        f.Matrix[0,1]=0f;rows=new[]{Row(1),Row(2)};Equal(rows,"Signed zeros remain distinct next to canonical NaNs");
        f.Counts[0]=3;f.Name="different";rows=new[]{Row(1),Row(2)};Equal(rows,"In-place source edits must invalidate cached content");
        Equal(new[]{Row(2)},"Deleted rows must not remain in cached fingerprints");
        Equal(new[]{Row(2),Row(3)},"Inserted rows and subsequent cached rows agree with full encoding");
        var baseline=state.Prepare(1,0,new[]{Row(1),Row(2)});var hash=state.Fingerprint(baseline);state.Commit(1,baseline,hash);checks++;
        f.Counts[0]=4;var changes=state.Changes(new[]{Row(1),Row(2)});var next=state.Prepare(2,1,changes);var nextHash=state.Fingerprint(next);state.Commit(2,next,nextHash);checks++;
        if(nextHash!=Original(next))throw new Exception("Committed incremental checksum differs from full encoding");checks++;
        f.Number=123;f.CountMatrix[0,0]=23;var altered=state.Prepare(3,2,new[]{Row(1)});bool rejected=false;
        try{state.Commit(3,altered,nextHash);}catch(InvalidDataException){rejected=true;}if(!rejected || state.Revision!=2 || state.Hash!=nextHash)throw new Exception("Cached checksums must still reject an altered commit transactionally");checks++;
        if(state.Changes(next).Length!=0)throw new Exception("A failed commit advanced the committed record baseline");checks++;
        var retry=state.Prepare(3,2,Array.Empty<MpStateRow>(),nextHash);
        if(state.Fingerprint(retry)!=Original(next))throw new Exception("A rejected commit contaminated later cached canonical bytes");checks++;
        string alteredHash=state.Fingerprint(altered);state.Commit(3,altered,alteredHash);
        if(state.Revision!=3 || state.Hash!=Original(altered) || state.Changes(altered).Length!=0)throw new Exception("A valid retry could not commit after a rejected commit");checks++;
        // Repeat the observed roster scale without Unity. This is a synthetic throughput
        // measurement, not a bound on native capture, network latency or game performance.
        f.Values=Enumerable.Range(0,96).Select(i=>(float)i).ToArray();f.Number=1;f.Matrix=new float[8,8];
        for(int i=0;i<8;i++)for(int j=0;j<8;j++)f.Matrix[i,j]=i*8+j;
        f.CountMatrix=null;f.FlagMatrix=null;f.TextMatrix=null;
        var large=Enumerable.Range(0,18000).Select(Row).ToArray();state.Fingerprint(large);
        long allocationStart=GC.GetAllocatedBytesForCurrentThread();
        var timer=Stopwatch.StartNew();var reference=Original(large);timer.Stop();double full=timer.Elapsed.TotalMilliseconds;long fullAllocation=GC.GetAllocatedBytesForCurrentThread()-allocationStart;
        allocationStart=GC.GetAllocatedBytesForCurrentThread();timer.Restart();var cached=state.Fingerprint(large);timer.Stop();double warm=timer.Elapsed.TotalMilliseconds;long warmAllocation=GC.GetAllocatedBytesForCurrentThread()-allocationStart;
        if(cached!=reference)throw new Exception("Large cached snapshot differs from full encoding");checks++;
        Console.WriteLine($"Synthetic 18,000-row fingerprint (96 floats and 8x8 matrix per row): full encoding {full:F1} ms / {fullAllocation/1048576.0:F2} MiB allocated; warm cache {warm:F1} ms / {warmAllocation/1048576.0:F2} MiB allocated; identical SHA-256. No Unity or network timing measured.");
        return checks;
    }
}
