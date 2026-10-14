using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace GTIP
{
    // Local schemas choose every writable field. The peer never supplies CLR types or field names.
    public sealed class MpStateSchema
    {
        public readonly FieldInfo[] Fields;
        public readonly string Signature;
        public readonly HashSet<string> NaNFields;
        public MpStateSchema(Type type, IEnumerable<string> names, IEnumerable<string> nanFields=null)
        {
            NaNFields=new HashSet<string>(nanFields??Array.Empty<string>(),StringComparer.Ordinal);
            Fields=names.OrderBy(n=>n,StringComparer.Ordinal).Select(n=>type.GetField(n,BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)
                ?? throw new InvalidOperationException("Missing state field: "+type.Name+"."+n)).ToArray();
            if(Fields.Any(f=>f.IsInitOnly || !Supported(f.FieldType))) throw new InvalidOperationException("Unsupported state schema.");
            Signature=type.Name+":"+string.Join(";",Fields.Select(f=>f.Name+":"+f.FieldType.FullName+(NaNFields.Contains(f.Name)?":nan":"")));
        }
        public static bool Supported(Type type) => type==typeof(int) || type==typeof(float) || type==typeof(bool) || type==typeof(string) ||
            (type.IsArray && type.GetArrayRank()<=2 && (type.GetElementType()==typeof(int) || type.GetElementType()==typeof(float) || type.GetElementType()==typeof(bool) || type.GetElementType()==typeof(string)));
        public static MpStateSchema Scalars(Type type, params string[] nanFields) => new MpStateSchema(type,type.GetFields(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)
            .Where(f=>!f.IsInitOnly && Supported(f.FieldType)).Select(f=>f.Name),nanFields);
        public object[] Capture(object target) => Fields.Select(f=>{var v=f.GetValue(target); return v is Array a?a.Clone():v;}).ToArray();
        public void Apply(object target,object[] values)
        {
            if(values.Length!=Fields.Length) throw new InvalidDataException("State field count mismatch.");
            for(int i=0;i<Fields.Length;i++) Fields[i].SetValue(target,values[i] is Array a?a.Clone():values[i]);
        }
    }
    public sealed class MpStateRow
    {
        public int Kind, Id, Current, Last;
        public bool Deleted;
        public string Name;
        public object[] Values;
    }
    public static class MpStateCodec
    {
        public const int MaxBytes=16*1024*1024, MaxRows=40000, MaxArray=20000;
        static readonly Encoding Utf8=new UTF8Encoding(false,true);
        public static string SchemaHash(MpStateSchema[] schemas)
        { using(var h=SHA256.Create()) return Convert.ToBase64String(h.ComputeHash(Utf8.GetBytes(string.Join("\n",schemas.Select(s=>s.Signature))))); }
        public static byte[] Encode(MpStateSchema[] schemas,MpStateRow[] rows)
        {
            if(rows.Length>MaxRows) throw new InvalidDataException("Too many state rows.");
            using(var raw=new MemoryStream()) {
                using(var w=new BinaryWriter(raw,Utf8,true)) {
                    Text(w,SchemaHash(schemas)); w.Write(rows.Length);
                    foreach(var row in rows) {
                        if(row.Kind<0 || row.Kind>=schemas.Length || row.Id<0) throw new InvalidDataException("Invalid state row.");
                        w.Write(row.Kind); w.Write(row.Id); w.Write(row.Current); w.Write(row.Last); Text(w,row.Name);
                        w.Write(row.Deleted);
                        if(row.Deleted)continue;
                        var fields=schemas[row.Kind].Fields;
                        if(row.Values.Length!=fields.Length) throw new InvalidDataException("State field count mismatch.");
                        for(int i=0;i<fields.Length;i++) {
                            try { Write(w,fields[i].FieldType,row.Values[i],schemas[row.Kind].NaNFields.Contains(fields[i].Name)); }
                            catch(InvalidDataException e) { throw new InvalidDataException("State "+row.Kind+":"+row.Id+" "+fields[i].Name+": "+e.Message,e); }
                        }
                        if(raw.Length>MaxBytes) throw new InvalidDataException("State exceeds byte limit.");
                    }
                }
                using(var packed=new MemoryStream()) {
                    using(var gzip=new GZipStream(packed,CompressionLevel.Fastest,true)) { raw.Position=0; raw.CopyTo(gzip); }
                    if(packed.Length>MaxBytes) throw new InvalidDataException("Compressed state exceeds byte limit.");
                    return packed.ToArray();
                }
            }
        }
        public static MpStateRow[] Decode(MpStateSchema[] schemas,byte[] bytes)
        {
            if(bytes==null || bytes.Length==0 || bytes.Length>MaxBytes) throw new InvalidDataException("Invalid state size.");
            using(var raw=new MemoryStream()) {
                using(var packed=new MemoryStream(bytes)) using(var gzip=new GZipStream(packed,CompressionMode.Decompress)) {
                    var buffer=new byte[8192]; int n;
                    while((n=gzip.Read(buffer,0,buffer.Length))>0) { if(raw.Length+n>MaxBytes) throw new InvalidDataException("Expanded state exceeds byte limit."); raw.Write(buffer,0,n); }
                }
                raw.Position=0;
                using(var r=new BinaryReader(raw,Utf8,true)) {
                    if(Text(r)!=SchemaHash(schemas)) throw new InvalidDataException("Campaign data schema mismatch.");
                    int count=Count(r,MaxRows); var rows=new MpStateRow[count]; var seen=new HashSet<string>();
                    for(int n=0;n<count;n++) {
                        var row=new MpStateRow {Kind=r.ReadInt32(),Id=r.ReadInt32(),Current=r.ReadInt32(),Last=r.ReadInt32(),Name=Text(r)};
                        if(row.Kind<0 || row.Kind>=schemas.Length || row.Id<0 || !seen.Add(row.Kind+":"+row.Id)) throw new InvalidDataException("Invalid or duplicate state row.");
                        row.Deleted=(bool)Read(r,typeof(bool));
                        if(row.Deleted){row.Values=Array.Empty<object>();rows[n]=row;continue;}
                        var fields=schemas[row.Kind].Fields; row.Values=new object[fields.Length];
                        for(int i=0;i<fields.Length;i++) row.Values[i]=Read(r,fields[i].FieldType,schemas[row.Kind].NaNFields.Contains(fields[i].Name));
                        rows[n]=row;
                    }
                    if(raw.Position!=raw.Length) throw new InvalidDataException("Trailing campaign state data.");
                    return rows;
                }
            }
        }
        static int Count(BinaryReader r,int max) { int n=r.ReadInt32(); if(n<0 || n>max) throw new InvalidDataException("Invalid collection size."); return n; }
        static void Text(BinaryWriter w,string value)
        { if(value==null) {w.Write(-1);return;} var b=Utf8.GetBytes(value); if(b.Length>65536) throw new InvalidDataException("State string too long."); w.Write(b.Length);w.Write(b); }
        static string Text(BinaryReader r)
        { int n=r.ReadInt32();if(n==-1)return null;if(n<0 || n>65536 || n>r.BaseStream.Length-r.BaseStream.Position)throw new InvalidDataException("Invalid state string.");return Utf8.GetString(r.ReadBytes(n)); }
        static void Write(BinaryWriter w,Type t,object v,bool allowNaN=false)
        {
            if(t==typeof(int)) w.Write((int)v);
            else if(t==typeof(float)) {float f=(float)v;if((!allowNaN && float.IsNaN(f))||float.IsInfinity(f))throw new InvalidDataException("Nonfinite state value.");w.Write(f);}
            else if(t==typeof(bool)) w.Write((bool)v);
            else if(t==typeof(string)) Text(w,(string)v);
            else {var a=(Array)v;w.Write(a!=null);if(a==null)return;if(a.Length>MaxArray)throw new InvalidDataException("State array too large.");for(int i=0;i<a.Rank;i++)w.Write(a.GetLength(i));foreach(var e in a)Write(w,t.GetElementType(),e,allowNaN);}
        }
        static object Read(BinaryReader r,Type t,bool allowNaN=false)
        {
            if(t==typeof(int))return r.ReadInt32();
            if(t==typeof(float)){float f=r.ReadSingle();if((!allowNaN && float.IsNaN(f))||float.IsInfinity(f))throw new InvalidDataException("Nonfinite state value.");return f;}
            if(t==typeof(bool)){byte b=r.ReadByte();if(b>1)throw new InvalidDataException("Invalid boolean.");return b==1;}
            if(t==typeof(string))return Text(r);
            if(!(bool)Read(r,typeof(bool)))return null;
            int rank=t.GetArrayRank();var sizes=new int[rank];long count=1;
            for(int i=0;i<rank;i++){sizes[i]=Count(r,MaxArray);count*=sizes[i];if(count>MaxArray)throw new InvalidDataException("State array too large.");}
            var a=Array.CreateInstance(t.GetElementType(),sizes);
            for(int i=0;i<count;i++){var v=Read(r,t.GetElementType(),allowNaN);if(rank==1)a.SetValue(v,i);else a.SetValue(v,i/sizes[1],i%sizes[1]);}return a;
        }
    }
}
