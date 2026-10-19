using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace GTIP
{
    // Pure replication state, independent of Unity. Prepare never advances the baseline.
    // The caller commits only after native application succeeds (guest) or its receipt arrives (host).
    public sealed class MpStateBaseline
    {
        readonly MpStateSchema[] schemas;
        Dictionary<long,MpStateRow> records=new Dictionary<long,MpStateRow>();
        sealed class EncodedRow {public MpStateRow Row;public byte[] Bytes;}
        Dictionary<long,EncodedRow> encodedRows=new Dictionary<long,EncodedRow>();
        public int Revision { get; private set; }
        public string Hash { get; private set; } = "";
        public MpStateBaseline(MpStateSchema[] schemas) { this.schemas=schemas; }
        static long Key(MpStateRow r)=>((long)r.Kind<<32)|(uint)r.Id;
        static MpStateRow Clone(MpStateRow r)=>new MpStateRow {Kind=r.Kind,Id=r.Id,Current=r.Current,Last=r.Last,Name=r.Name,Deleted=r.Deleted,
            Values=r.Values?.Select(v=>v is Array a?a.Clone():v).ToArray()};
        static bool SameValue(object a,object b)
        {
            if(a is float f && b is float g)return SameFloat(f,g);
            if(a is int[] ai && b is int[] bi)return ai.SequenceEqual(bi);
            if(a is float[] af && b is float[] bf){if(af.Length!=bf.Length)return false;for(int i=0;i<af.Length;i++)if(!SameFloat(af[i],bf[i]))return false;return true;}
            if(a is bool[] ab && b is bool[] bb)return ab.SequenceEqual(bb);
            if(a is string[] at && b is string[] bt)return at.SequenceEqual(bt);
            if(a is float[,] amf && b is float[,] bmf) {
                if(!SameShape(amf,bmf))return false;
                for(int i=0;i<amf.GetLength(0);i++)for(int j=0;j<amf.GetLength(1);j++)if(!SameFloat(amf[i,j],bmf[i,j]))return false;return true;
            }
            if(a is int[,] ami && b is int[,] bmi) {
                if(!SameShape(ami,bmi))return false;
                for(int i=0;i<ami.GetLength(0);i++)for(int j=0;j<ami.GetLength(1);j++)if(ami[i,j]!=bmi[i,j])return false;return true;
            }
            if(a is bool[,] amb && b is bool[,] bmb) {
                if(!SameShape(amb,bmb))return false;
                for(int i=0;i<amb.GetLength(0);i++)for(int j=0;j<amb.GetLength(1);j++)if(amb[i,j]!=bmb[i,j])return false;return true;
            }
            if(a is string[,] ams && b is string[,] bms) {
                if(!SameShape(ams,bms))return false;
                for(int i=0;i<ams.GetLength(0);i++)for(int j=0;j<ams.GetLength(1);j++)if(ams[i,j]!=bms[i,j])return false;return true;
            }
            if(a is Array x && b is Array y) {
                if(x.Rank!=y.Rank || Enumerable.Range(0,x.Rank).Any(i=>x.GetLength(i)!=y.GetLength(i)))return false;
                return x.Cast<object>().Zip(y.Cast<object>(),SameValue).All(v=>v);
            }
            return Equals(a,b);
        }
        static bool SameFloat(float a,float b)=>a.Equals(b) && (a!=0f || (1f/a).Equals(1f/b));
        static bool SameShape(Array a,Array b)=>a.GetLength(0)==b.GetLength(0) && a.GetLength(1)==b.GetLength(1);
        static bool Same(MpStateRow a,MpStateRow b)=>a.Current==b.Current && a.Last==b.Last && a.Name==b.Name &&
            SameValues(a.Values,b.Values);
        static bool SameValues(object[] a,object[] b)
        {
            if(a.Length!=b.Length)return false;
            for(int i=0;i<a.Length;i++)if(!SameValue(a[i],b[i]))return false;
            return true;
        }
        public static string FirstDifference(MpStateSchema schema,object[] expected,object[] actual)
        {
            if(expected.Length!=actual.Length || expected.Length!=schema.Fields.Length)return "field count";
            for(int i=0;i<expected.Length;i++)if(!SameValue(expected[i],actual[i]))return schema.Fields[i].Name;
            return null;
        }
        Dictionary<long,MpStateRow> Index(IEnumerable<MpStateRow> rows,bool allowDeleted)
        {
            var result=new Dictionary<long,MpStateRow>();
            foreach(var r in rows) {
                if(r==null || r.Kind<0 || r.Kind>=schemas.Length || r.Id<0 || (!allowDeleted && r.Deleted) ||
                    (!r.Deleted && (r.Values==null || r.Values.Length!=schemas[r.Kind].Fields.Length)) || result.ContainsKey(Key(r)))
                    throw new InvalidDataException("Invalid replication record.");
                result.Add(Key(r),Clone(r));
                if(result.Count>MpStateCodec.MaxRows)throw new InvalidDataException("Replication roster limit exceeded.");
            }
            return result;
        }
        public MpStateRow[] Changes(MpStateRow[] current)
        {
            var next=Index(current,false);
            var changes=next.Where(p=>!records.TryGetValue(p.Key,out var old)||!Same(old,p.Value)).Select(p=>Clone(p.Value)).ToList();
            changes.AddRange(records.Where(p=>!next.ContainsKey(p.Key)).Select(p=>new MpStateRow{Kind=p.Value.Kind,Id=p.Value.Id,Deleted=true,Values=Array.Empty<object>()}));
            return changes.OrderBy(Key).ToArray();
        }
        public MpStateRow[] Prepare(int revision,int baseRevision,MpStateRow[] changes,string expectedHash=null)
        {
            if(baseRevision!=Revision || revision!=checked(Revision+1))throw new InvalidDataException("Campaign revision mismatch: local="+Revision+", base="+baseRevision+", incoming="+revision);
            var next=records.ToDictionary(p=>p.Key,p=>Clone(p.Value));
            foreach(var p in Index(changes,true)) {
                if(p.Value.Deleted) {
                    if(!next.Remove(p.Key))throw new InvalidDataException("Deletion references an absent record: "+p.Value.Kind+":"+p.Value.Id);
                } else next[p.Key]=p.Value;
            }
            if(next.Count>MpStateCodec.MaxRows)throw new InvalidDataException("Replication roster limit exceeded.");
            var rows=next.OrderBy(p=>p.Key).Select(p=>p.Value).ToArray();
            if(expectedHash!=null && Fingerprint(rows)!=expectedHash)throw new InvalidDataException("Campaign revision checksum mismatch.");
            return rows;
        }
        public string Fingerprint(MpStateRow[] rows)
        {
            // Preserve the exact full-snapshot byte stream/checksum. Only encoding of
            // unchanged immutable records is reused; validation and commit remain intact.
            var next=new Dictionary<long,EncodedRow>();
            using(var hash=SHA256.Create()) {
                byte[] header;
                using(var raw=new MemoryStream())using(var writer=new BinaryWriter(raw,System.Text.Encoding.UTF8,true)) {
                    MpStateCodec.WriteHeader(writer,schemas,rows.Length);writer.Flush();header=raw.ToArray();
                }
                hash.TransformBlock(header,0,header.Length,header,0);long length=header.Length;
                foreach(var row in rows.OrderBy(Key)) {
                    long key=Key(row);
                    if(row.Deleted || !encodedRows.TryGetValue(key,out var cached) || cached.Row.Deleted || !Same(cached.Row,row)) {
                        using(var single=new MemoryStream())using(var w=new BinaryWriter(single,System.Text.Encoding.UTF8,true)) {
                            MpStateCodec.WriteRow(w,schemas,row);w.Flush();
                            cached=new EncodedRow{Row=Clone(row),Bytes=single.ToArray()};
                        }
                    }
                    length+=cached.Bytes.Length;
                    if(length>MpStateCodec.MaxBytes)throw new InvalidDataException("State exceeds byte limit.");
                    // Hash the exact canonical bytes directly. Avoid buffering and
                    // copying a second complete campaign for each checksum.
                    hash.TransformBlock(cached.Bytes,0,cached.Bytes.Length,cached.Bytes,0);next[key]=cached;
                }
                hash.TransformFinalBlock(Array.Empty<byte>(),0,0);encodedRows=next;return Convert.ToBase64String(hash.Hash);
            }
        }
        public void Commit(int revision,MpStateRow[] rows,string expectedHash)
        {
            if(revision!=checked(Revision+1))throw new InvalidDataException("Cannot commit a stale campaign revision.");
            var next=Index(rows,false);
            var actual=Fingerprint(next.OrderBy(p=>p.Key).Select(p=>p.Value).ToArray());
            if(actual!=expectedHash)throw new InvalidDataException("Cannot commit a mismatched campaign revision.");
            records=next;Revision=revision;Hash=actual;
        }
    }
}
