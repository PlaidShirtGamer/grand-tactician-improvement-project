using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GTIP
{
    // Pure replication state, independent of Unity. Prepare never advances the baseline.
    // The caller commits only after native application succeeds (guest) or its receipt arrives (host).
    public sealed class MpStateBaseline
    {
        readonly MpStateSchema[] schemas;
        Dictionary<long,MpStateRow> records=new Dictionary<long,MpStateRow>();
        public int Revision { get; private set; }
        public string Hash { get; private set; } = "";
        public MpStateBaseline(MpStateSchema[] schemas) { this.schemas=schemas; }
        static long Key(MpStateRow r)=>((long)r.Kind<<32)|(uint)r.Id;
        static MpStateRow Clone(MpStateRow r)=>new MpStateRow {Kind=r.Kind,Id=r.Id,Current=r.Current,Last=r.Last,Name=r.Name,Deleted=r.Deleted,
            Values=r.Values?.Select(v=>v is Array a?a.Clone():v).ToArray()};
        static bool SameValue(object a,object b)
        {
            if(a is Array x && b is Array y) {
                if(x.Rank!=y.Rank || Enumerable.Range(0,x.Rank).Any(i=>x.GetLength(i)!=y.GetLength(i)))return false;
                return x.Cast<object>().SequenceEqual(y.Cast<object>());
            }
            return Equals(a,b);
        }
        static bool Same(MpStateRow a,MpStateRow b)=>a.Current==b.Current && a.Last==b.Last && a.Name==b.Name &&
            a.Values.Length==b.Values.Length && a.Values.Zip(b.Values,SameValue).All(v=>v);
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
        public string Fingerprint(MpStateRow[] rows)=>MpCheckpoint.Hash(MpStateCodec.Encode(schemas,rows.OrderBy(Key).ToArray(),false));
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
