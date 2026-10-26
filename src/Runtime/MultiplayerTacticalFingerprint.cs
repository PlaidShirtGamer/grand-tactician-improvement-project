using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GTIP
{
    // Explicit initialized-world records, not save-file or Unity-object hashes.
    // Each kind has a fixed field order defined by the native capture adapter.
    public sealed class MpTacticalFingerprintRecord
    {
        public string Kind, Key, Parent;
        public string[] Text;
        public int[] Integers;
        public bool[] Flags;
        public float[] Values;
    }
    public static class MpTacticalFingerprint
    {
        const int MaxRecords=150000, MaxArray=4096, MaxTextBytes=16384, MaxBytes=64*1024*1024;
        static readonly Encoding Utf8=new UTF8Encoding(false,true);
        static readonly HashSet<string> Kinds=new HashSet<string>(new[]{"world","unit","pose","block","objective","officer","side"},StringComparer.Ordinal);
        public static string Segment(int alliance,int type,int commander,string name)
        {
            name=name??"";
            return alliance.ToString(CultureInfo.InvariantCulture)+":"+type.ToString(CultureInfo.InvariantCulture)+":"+
                commander.ToString(CultureInfo.InvariantCulture)+":"+name.Length.ToString(CultureInfo.InvariantCulture)+":"+name;
        }
        // Fixed bins are deliberate. Geometry is millimetre/millidegree precision;
        // gameplay scalars use 1e-5. This is not an arbitrary pairwise epsilon test.
        public static int Angle(float degrees)
        {
            Finite(degrees);double turn=(double)degrees%360d;if(turn<0)turn+=360d;
            return (int)Math.Round(turn*1000d,MidpointRounding.AwayFromZero)%360000;
        }
        public static string Hash(IEnumerable<MpTacticalFingerprintRecord> source)
        {
            var records=Ordered(source);
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Utf8)) {
                writer.Write(Encoding.ASCII.GetBytes("GTIP-BATTLE-WORLD-1"));writer.Write(records.Length);
                foreach(var record in records) {
                    WriteRecord(writer,record);
                    if(stream.Length>MaxBytes)throw new InvalidDataException("Initialized tactical baseline exceeds its bound.");
                }
                using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(stream.ToArray()));
            }
        }
        public static string FirstDifference(IEnumerable<MpTacticalFingerprintRecord> left,IEnumerable<MpTacticalFingerprintRecord> right)
        {
            var a=Ordered(left);var b=Ordered(right);
            if(a.Length!=b.Length)return "record count "+a.Length+" / "+b.Length;
            for(int i=0;i<a.Length;i++) {
                if(a[i].Kind!=b[i].Kind || a[i].Key!=b[i].Key)return "identity "+a[i].Kind+":"+a[i].Key+" / "+b[i].Kind+":"+b[i].Key;
                using(var x=new MemoryStream())using(var y=new MemoryStream())using(var wx=new BinaryWriter(x,Utf8))using(var wy=new BinaryWriter(y,Utf8)) {
                    WriteRecord(wx,a[i]);WriteRecord(wy,b[i]);
                    if(!x.ToArray().SequenceEqual(y.ToArray()))return a[i].Kind+":"+a[i].Key;
                }
            }
            return null;
        }
        static MpTacticalFingerprintRecord[] Ordered(IEnumerable<MpTacticalFingerprintRecord> source)
        {
            if(source==null)throw new InvalidDataException("Missing initialized tactical baseline.");
            var list=new List<MpTacticalFingerprintRecord>();
            foreach(var record in source) {
                if(record==null || !Kinds.Contains(record.Kind??"") || string.IsNullOrEmpty(record.Key))throw new InvalidDataException("Invalid tactical baseline record.");
                if(list.Count>=MaxRecords)throw new InvalidDataException("Too many initialized tactical records.");
                list.Add(record);
            }
            var records=list.OrderBy(r=>r.Kind,StringComparer.Ordinal).ThenBy(r=>r.Key,StringComparer.Ordinal).ToArray();
            for(int i=1;i<records.Length;i++)if(records[i].Kind==records[i-1].Kind && records[i].Key==records[i-1].Key)
                throw new InvalidDataException("Ambiguous initialized tactical identity: "+records[i].Kind+":"+records[i].Key);
            var parents=records.Where(r=>r.Kind=="unit").ToDictionary(r=>r.Key,r=>r.Parent??"",StringComparer.Ordinal);
            foreach(var key in parents.Keys) {
                string node=key;int depth=0;
                while(node.Length!=0) {
                    if(!parents.TryGetValue(node,out node))throw new InvalidDataException("Missing initialized tactical parent.");
                    if(++depth>64)throw new InvalidDataException("Cyclic or excessive initialized tactical hierarchy.");
                }
            }
            return records;
        }
        static void WriteRecord(BinaryWriter writer,MpTacticalFingerprintRecord r)
        {
            Text(writer,r.Kind);Text(writer,r.Key);Text(writer,r.Parent??"");
            Count(writer,r.Text);if(r.Text!=null)foreach(string text in r.Text)Text(writer,text);
            Count(writer,r.Integers);if(r.Integers!=null)foreach(int number in r.Integers)writer.Write(number);
            Count(writer,r.Flags);if(r.Flags!=null)foreach(bool flag in r.Flags)writer.Write(flag);
            Count(writer,r.Values);
            double resolution=(r.Kind=="pose" || r.Kind=="block")?1000d:100000d;
            if(r.Values!=null)foreach(float value in r.Values) {
                Finite(value);double scaled=Math.Round(value*resolution,MidpointRounding.AwayFromZero);
                if(scaled>=9223372036854775808d || scaled< -9223372036854775808d)throw new InvalidDataException("Initialized tactical value exceeds its bound.");
                writer.Write((long)scaled);
            }
        }
        static void Count(BinaryWriter writer,Array values)
        {if(values!=null && values.Length>MaxArray)throw new InvalidDataException("Initialized tactical array exceeds its bound.");writer.Write(values==null?-1:values.Length);}
        static void Text(BinaryWriter writer,string text)
        {if(text==null){writer.Write(-1);return;}var bytes=Utf8.GetBytes(text);if(bytes.Length>MaxTextBytes)throw new InvalidDataException("Initialized tactical text exceeds its bound.");writer.Write(bytes.Length);writer.Write(bytes);}
        static void Finite(float value)
        {if(float.IsNaN(value) || float.IsInfinity(value))throw new InvalidDataException("Nonfinite initialized tactical value.");}
    }
}
