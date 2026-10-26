using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace GTIP
{
    // Explicit data-only campaign files. No executable files or peer-controlled paths.
    public static class MpCheckpoint
    {
        public const int MaxBytes=64*1024*1024, ChunkBytes=48*1024;
        static readonly HashSet<string> Allowed=new HashSet<string>(("addcommanders.dat AICampaign.dat AlarmInfos.dat AllObjects.dat armygrouprefs.dat armygroups.dat attributes.dat battledata.dat buildings.dat CampaignObjectives.dat ChartDatabase.dat commanders.txt garrisonrefs.dat groups.dat IIPsTowns.dat nations.dat paths.dat Railroads.dat regiments.dat scenario.dat ships.dat shiptype.dat snow.dat spottings.dat version.dat weapons.dat weather.dat").Split(' '),StringComparer.OrdinalIgnoreCase);
        static readonly string[] Required={"scenario.dat","groups.dat","regiments.dat","commanders.txt","nations.dat","AICampaign.dat","version.dat"};
        public static string Hash(byte[] bytes) { using(var sha=SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(bytes)); }
        public static byte[] Pack(string directory)
        {
            var files=Directory.GetFiles(directory).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            foreach(var file in files) if(!Allowed.Contains(Path.GetFileName(file))) throw new InvalidDataException("Unrecognized checkpoint file: "+Path.GetFileName(file));
            foreach(var name in Required) if(!files.Any(p=>string.Equals(Path.GetFileName(p),name,StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Checkpoint missing "+name);
            using(var output=new MemoryStream()) {
                using(var zip=new GZipStream(output,CompressionMode.Compress,true)) using(var writer=new BinaryWriter(zip,Encoding.UTF8)) {
                    writer.Write(files.Length); long total=0;
                    foreach(var file in files) {
                        long size=new FileInfo(file).Length; total+=size;
                        if(size>MaxBytes || total>MaxBytes) throw new InvalidDataException("Checkpoint exceeds 64 MiB.");
                        var data=File.ReadAllBytes(file);
                        if(total>MaxBytes) throw new InvalidDataException("Checkpoint exceeds 64 MiB.");
                        writer.Write(Path.GetFileName(file)); writer.Write(data.Length); writer.Write(data);
                    }
                }
                if(output.Length>MaxBytes) throw new InvalidDataException("Compressed checkpoint too large.");
                return output.ToArray();
            }
        }
        public static void Unpack(byte[] data,string expectedHash,string newDirectory)
        {
            if(data==null || data.Length>MaxBytes || Hash(data)!=expectedHash) throw new InvalidDataException("Checkpoint checksum mismatch.");
            if(Directory.Exists(newDirectory)) throw new IOException("Checkpoint target already exists.");
            var entries=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
            using(var input=new MemoryStream(data)) using(var zip=new GZipStream(input,CompressionMode.Decompress)) using(var reader=new BinaryReader(zip,Encoding.UTF8)) {
                int count=reader.ReadInt32(); long total=0;
                if(count<Required.Length || count>Allowed.Count) throw new InvalidDataException("Invalid checkpoint file count.");
                for(int i=0;i<count;i++) {
                    // Bound filename before allocation (BinaryWriter's short UTF-8 name prefix).
                    int size=reader.ReadByte();
                    if(size<1 || size>100) throw new InvalidDataException("Invalid checkpoint filename length.");
                    var name=Encoding.UTF8.GetString(reader.ReadBytes(size));
                    if(!Allowed.Contains(name) || entries.ContainsKey(name)) throw new InvalidDataException("Unexpected checkpoint filename: "+name);
                    int length=reader.ReadInt32(); total+=length;
                    if(length<0 || total>MaxBytes) throw new InvalidDataException("Invalid checkpoint size.");
                    var bytes=reader.ReadBytes(length); if(bytes.Length!=length) throw new EndOfStreamException("Incomplete checkpoint.");
                    entries.Add(name,bytes);
                }
                if(reader.BaseStream.ReadByte()!=-1) throw new InvalidDataException("Trailing checkpoint data.");
            }
            foreach(var name in Required) if(!entries.ContainsKey(name)) throw new InvalidDataException("Checkpoint missing "+name);
            Directory.CreateDirectory(newDirectory);
            foreach(var entry in entries) File.WriteAllBytes(Path.Combine(newDirectory,entry.Key),entry.Value);
        }
    }
}
