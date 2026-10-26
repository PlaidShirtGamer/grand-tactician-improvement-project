using System;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Globalization;

namespace GTIP
{
    public static class MpBuild
    {
        public const string Version = "0.39";
        public const string Label = "GTIP MP Test v" + Version;
    }
    public static class MpIdentity
    {
        // Length-prefixed components cannot collide when names contain separators.
        public static string Segment(int side, int tier, int commander, string name)
        {
            name = name ?? "";
            return side.ToString(CultureInfo.InvariantCulture)+":"+tier.ToString(CultureInfo.InvariantCulture)+":"+
                commander.ToString(CultureInfo.InvariantCulture)+":"+name.Length.ToString(CultureInfo.InvariantCulture)+":"+name;
        }
        public static string[] Order(string[] keys)
        {
            var ordered = keys.OrderBy(k=>k, StringComparer.Ordinal).ToArray();
            for(int i=1;i<ordered.Length;i++) if(ordered[i]==ordered[i-1])
                throw new InvalidOperationException("Ambiguous formation identity: " + ordered[i]);
            return ordered;
        }
        public static string Hash(string[] ordered)
        {
            using(var hash=SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(
                string.Concat(ordered.Select(k=>k.Length.ToString(CultureInfo.InvariantCulture)+":"+k)))));
        }
        public static MpUnit HierarchyRecord(string[] checkpointKeys, int id, int parent)
        {
            if(id<1 || id>checkpointKeys.Length) throw new InvalidOperationException("Unknown session formation ID: "+id);
            // Names and commanders may change. Identity is captured once at handshake.
            return new MpUnit { id=id, parent=parent, name=checkpointKeys[id-1] };
        }
        public static void ValidateHierarchy(MpUnit[] records, string[] keys)
        {
            if(records==null || records.Length!=keys.Length) throw new InvalidOperationException("Incomplete host hierarchy.");
            var parents=new int[keys.Length+1]; var seen=new bool[keys.Length+1];
            foreach(var row in records) {
                if(row==null) throw new InvalidOperationException("Null host hierarchy record.");
                if(row.id<1 || row.id>keys.Length) throw new InvalidOperationException("Unknown host formation ID: "+row.id);
                if(seen[row.id]) throw new InvalidOperationException("Duplicate host formation ID: "+row.id);
                if(row.name!=keys[row.id-1]) throw new InvalidOperationException("Session identity changed for formation "+row.id+": expected "+keys[row.id-1]+"; received "+row.name);
                if(row.parent<0 || row.parent>keys.Length) throw new InvalidOperationException("Unknown parent "+row.parent+" for formation "+row.id);
                seen[row.id]=true; parents[row.id]=row.parent;
            }
            for(int i=1;i<parents.Length;i++) {
                int node=i, steps=0;
                while(node!=0) { if(++steps>keys.Length) throw new InvalidOperationException("Cyclic host hierarchy."); node=parents[node]; }
            }
        }
        public static string Difference(string[] host, string[] guest)
        {
            guest=Order(guest);
            var missing=host.Except(guest,StringComparer.Ordinal).FirstOrDefault();
            var extra=guest.Except(host,StringComparer.Ordinal).FirstOrDefault();
            if(missing==null && extra==null) return null;
            Func<string,string> shortKey=k=>k==null?"none":k.Substring(0,Math.Min(k.Length,350));
            return "Formation mismatch (host="+host.Length+", guest="+guest.Length+"). Host-only: "+shortKey(missing)+"; guest-only: "+shortKey(extra);
        }
    }
}
