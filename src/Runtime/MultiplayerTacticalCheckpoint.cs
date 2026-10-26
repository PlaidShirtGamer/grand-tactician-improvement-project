using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GTIP
{
    // Native non-DLC campaign save plus its Battle subdirectory. The wire format
    // cannot name an executable, a scene asset, or a path outside this allowlist.
    public static class MpTacticalCheckpoint
    {
        public const int MaxBytes = 64 * 1024 * 1024, ChunkBytes = 48 * 1024;
        static readonly byte[] Magic = Encoding.ASCII.GetBytes("GTIPBT01");
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        static readonly string[] CampaignFiles = ("addcommanders.dat AICampaign.dat AlarmInfos.dat AllObjects.dat armygrouprefs.dat armygroups.dat attributes.dat battledata.dat buildings.dat CampaignObjectives.dat ChartDatabase.dat commanders.txt garrisonrefs.dat groups.dat IIPsTowns.dat nations.dat paths.dat Railroads.dat regiments.dat scenario.dat ships.dat shiptype.dat snow.dat spottings.dat version.dat weapons.dat weather.dat").Split(' ');
        static readonly string[] BattleFiles = ("armygrouprefs.dat armygroups.dat artillery.dat attributes.dat battledata.dat battleunits.dat buildings.dat commanders.txt entrypoints.dat fallenlayers.dat frontlines.dat groups.dat objectives.dat obstacles.dat paths.dat pontoons.dat regiments.dat scenario.dat sideinfo.dat version.dat").Split(' ');
        static readonly string[] RequiredCampaign = ("scenario.dat groups.dat regiments.dat commanders.txt nations.dat AICampaign.dat version.dat").Split(' ');
        // Native SaveBattle writes these files even when their record count is zero.
        // Army groups are conditional on the game's allowarmygroups option.
        static readonly string[] RequiredBattle = BattleFiles.Where(n => n != "armygroups.dat").Select(n => "Battle/" + n).ToArray();
        static readonly Dictionary<string, string> Allowed = CampaignFiles.Concat(BattleFiles.Select(n => "Battle/" + n)).ToDictionary(n => n, n => n, StringComparer.OrdinalIgnoreCase);

        public static string Hash(byte[] bytes)
        {
            if (bytes == null) throw new InvalidDataException("Missing tactical checkpoint.");
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(bytes));
        }

        static string CanonicalName(string name)
        {
            if (name == null || !Allowed.TryGetValue(name, out var canonical))
                throw new InvalidDataException("Unrecognized tactical checkpoint file: " + name);
            return canonical;
        }

        static void Required(IEnumerable<string> names)
        {
            var present = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            foreach (var name in RequiredCampaign.Concat(RequiredBattle))
                if (!present.Contains(name)) throw new InvalidDataException("Tactical checkpoint missing " + name);
        }

        static void CheckNoLinks(string path)
        {
            for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Tactical checkpoint path contains a symbolic link or junction: " + current);
        }

        public static byte[] Pack(string campaignSaveDirectory)
        {
            var root = Path.GetFullPath(campaignSaveDirectory);
            CheckNoLinks(root);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Tactical checkpoint source does not exist.");
            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in Directory.GetFiles(root)) AddSource(paths, Path.GetFileName(path), path);
            foreach (var directory in Directory.GetDirectories(root))
            {
                if (!string.Equals(Path.GetFileName(directory), "Battle", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Unexpected tactical checkpoint directory: " + Path.GetFileName(directory));
                CheckNoLinks(directory);
                if (Directory.GetDirectories(directory).Length != 0) throw new InvalidDataException("Nested tactical checkpoint directories are not supported.");
                foreach (var path in Directory.GetFiles(directory)) AddSource(paths, "Battle/" + Path.GetFileName(path), path);
            }
            Required(paths.Keys);
            using (var output = new MemoryStream())
            {
                using (var zip = new GZipStream(output, CompressionMode.Compress, true))
                using (var writer = new BinaryWriter(zip, Utf8))
                {
                    writer.Write(Magic); writer.Write(paths.Count);
                    long total = 0;
                    foreach (var pair in paths.OrderBy(p => p.Key, StringComparer.Ordinal))
                    {
                        CheckNoLinks(pair.Value);
                        byte[] bytes;
                        using (var source = new FileStream(pair.Value, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var length = source.Length;
                            if (length > MaxBytes || total + length > MaxBytes) throw new InvalidDataException("Tactical checkpoint exceeds 64 MiB.");
                            using (var reader = new BinaryReader(source))
                            {
                                bytes = reader.ReadBytes((int)length);
                                if (bytes.Length != length || source.ReadByte() != -1) throw new IOException("Tactical checkpoint source changed during capture.");
                            }
                        }
                        total += bytes.Length;
                        if (total > MaxBytes) throw new InvalidDataException("Tactical checkpoint changed or exceeds 64 MiB.");
                        writer.Write(pair.Key); writer.Write(bytes.Length); writer.Write(bytes);
                    }
                }
                if (output.Length > MaxBytes) throw new InvalidDataException("Compressed tactical checkpoint exceeds 64 MiB.");
                return output.ToArray();
            }
        }

        static void AddSource(Dictionary<string, string> paths, string name, string path)
        {
            var canonical = CanonicalName(name);
            if (paths.ContainsKey(canonical)) throw new InvalidDataException("Duplicate tactical checkpoint file: " + name);
            CheckNoLinks(path); paths.Add(canonical, path);
        }

        public static void Unpack(byte[] bytes, string expectedHash, string newDirectory)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxBytes || Hash(bytes) != expectedHash)
                throw new InvalidDataException("Tactical checkpoint checksum or size mismatch.");
            var target = Path.GetFullPath(newDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parent = Path.GetDirectoryName(target);
            if (parent == null || !Directory.Exists(parent)) throw new IOException("Tactical checkpoint target parent does not exist.");
            CheckNoLinks(target);
            if (File.Exists(target) || Directory.Exists(target)) throw new IOException("Tactical checkpoint target already exists.");
            var entries = Decode(bytes);
            // Validate every byte and name before creating any visible save. The
            // final same-parent rename never overwrites a prior save directory.
            var stage = Path.Combine(parent, ".gtip-battle-stage-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(stage);
                foreach (var entry in entries)
                {
                    var path = Path.Combine(stage, entry.Key.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, entry.Value);
                }
                CheckNoLinks(target);
                Directory.Move(stage, target);
            }
            finally
            {
                // stage is a locally generated direct child of the validated
                // parent, never a peer-supplied extraction path.
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
            }
        }

        static Dictionary<string, byte[]> Decode(byte[] bytes)
        {
            var entries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            using (var input = new MemoryStream(bytes))
            using (var zip = new GZipStream(input, CompressionMode.Decompress))
            using (var reader = new BinaryReader(zip, Utf8))
            {
                if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic)) throw new InvalidDataException("Unsupported tactical checkpoint format.");
                var count = reader.ReadInt32();
                if (count < RequiredCampaign.Length + RequiredBattle.Length || count > Allowed.Count)
                    throw new InvalidDataException("Invalid tactical checkpoint file count.");
                long total = 0;
                for (var i = 0; i < count; i++)
                {
                    // All allowlisted UTF-8 names fit BinaryWriter's one-byte
                    // string length prefix. Reject longer names before allocation.
                    var nameLength = reader.ReadByte();
                    if (nameLength == 0 || nameLength > 100) throw new InvalidDataException("Invalid tactical checkpoint filename length.");
                    var nameBytes = reader.ReadBytes(nameLength);
                    if (nameBytes.Length != nameLength) throw new EndOfStreamException("Truncated tactical checkpoint filename.");
                    var name = CanonicalName(Utf8.GetString(nameBytes));
                    if (entries.ContainsKey(name)) throw new InvalidDataException("Duplicate tactical checkpoint filename: " + name);
                    var length = reader.ReadInt32();
                    total += length;
                    if (length < 0 || total > MaxBytes) throw new InvalidDataException("Invalid tactical checkpoint file size.");
                    var content = reader.ReadBytes(length);
                    if (content.Length != length) throw new EndOfStreamException("Truncated tactical checkpoint file.");
                    entries.Add(name, content);
                }
                if (reader.BaseStream.ReadByte() != -1) throw new InvalidDataException("Trailing tactical checkpoint data.");
            }
            Required(entries.Keys);
            return entries;
        }
    }
}
