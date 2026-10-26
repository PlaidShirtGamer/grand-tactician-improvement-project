using GTIP;
using System.IO.Compression;
using System.Text;

var campaign = "scenario.dat groups.dat regiments.dat commanders.txt nations.dat AICampaign.dat version.dat".Split(' ');
var battle = "armygrouprefs.dat artillery.dat attributes.dat battledata.dat battleunits.dat buildings.dat commanders.txt entrypoints.dat fallenlayers.dat frontlines.dat groups.dat objectives.dat obstacles.dat paths.dat pontoons.dat regiments.dat scenario.dat sideinfo.dat version.dat".Split(' ');
var required = campaign.Concat(battle.Select(n => "Battle/" + n)).ToArray();
var sandbox = Path.Combine(Environment.CurrentDirectory, "artifacts", "tactical-checkpoint-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(sandbox);
var checks = 0;
void Check(bool value, string text) { if (!value) throw new Exception(text); checks++; Console.WriteLine("PASS " + text); }
void Reject(Action action, string text)
{
    var rejected = false;
    try { action(); } catch (InvalidDataException) { rejected = true; } catch (IOException) { rejected = true; } catch (DecoderFallbackException) { rejected = true; }
    Check(rejected, text);
}
string Fixture(string name)
{
    var directory = Path.Combine(sandbox, name); Directory.CreateDirectory(Path.Combine(directory, "Battle"));
    foreach (var path in required)
        File.WriteAllBytes(Path.Combine(directory, path), path.EndsWith("frontlines.dat") ? Array.Empty<byte>() : Encoding.UTF8.GetBytes("native-data " + path + "\r\n0\r\n"));
    return directory;
}
List<KeyValuePair<string, byte[]>> Entries() => required.Select(n => new KeyValuePair<string, byte[]>(n, Encoding.UTF8.GetBytes("native-" + n))).ToList();
byte[] Archive(List<KeyValuePair<string, byte[]>> entries, int? overrideCount = null, int? firstLength = null, bool trailing = false, string magic = "GTIPBT01")
{
    using var output = new MemoryStream();
    using (var zip = new GZipStream(output, CompressionMode.Compress, true))
    using (var writer = new BinaryWriter(zip, Encoding.UTF8))
    {
        writer.Write(Encoding.ASCII.GetBytes(magic)); writer.Write(overrideCount ?? entries.Count);
        for (var i = 0; i < entries.Count; i++)
        { writer.Write(entries[i].Key); writer.Write(i == 0 && firstLength.HasValue ? firstLength.Value : entries[i].Value.Length); writer.Write(entries[i].Value); }
        if (trailing) writer.Write((byte)12);
    }
    return output.ToArray();
}
void RejectArchive(byte[] payload, string name)
{
    var target = Path.Combine(sandbox, "reject-" + name);
    Reject(() => MpTacticalCheckpoint.Unpack(payload, MpTacticalCheckpoint.Hash(payload), target), name + " is rejected");
    Check(!Directory.Exists(target), name + " never creates a visible partial save");
}

var source = Fixture("source");
File.WriteAllBytes(Path.Combine(source, "Battle/armygroups.dat"), new byte[] { 0, 255, 127, 1 });
var packed = MpTacticalCheckpoint.Pack(source);
var hash = MpTacticalCheckpoint.Hash(packed);
var target = Path.Combine(sandbox, "roundtrip");
MpTacticalCheckpoint.Unpack(packed, hash, target);
Check(Directory.GetFiles(target, "*", SearchOption.AllDirectories).Length == required.Length + 1, "Native campaign and Battle files extract together");
Check(Directory.GetFiles(source, "*", SearchOption.AllDirectories).All(p => File.ReadAllBytes(p).SequenceEqual(File.ReadAllBytes(Path.Combine(target, Path.GetRelativePath(source, p))))), "Archive preserves every file byte including binary and empty files");
Check(File.ReadAllBytes(Path.Combine(target, "Battle/commanders.txt")).SequenceEqual(File.ReadAllBytes(Path.Combine(source, "Battle/commanders.txt"))) && File.Exists(Path.Combine(target, "commanders.txt")), "Campaign and tactical commander tables retain separate paths");
Check(hash == MpTacticalCheckpoint.Hash(MpTacticalCheckpoint.Pack(source)), "Repeated capture of an unchanged native save has stable bytes and hash");
Check(!Directory.GetDirectories(sandbox, ".gtip-battle-stage-*").Any(), "Successful atomic extraction leaves no staging directory");
var noArmyGroups = Fixture("no-armygroups");
var noGroupsPayload = MpTacticalCheckpoint.Pack(noArmyGroups);
MpTacticalCheckpoint.Unpack(noGroupsPayload, MpTacticalCheckpoint.Hash(noGroupsPayload), Path.Combine(sandbox, "no-armygroups-extracted"));
Check(!File.Exists(Path.Combine(sandbox, "no-armygroups-extracted", "Battle/armygroups.dat")), "Native saves without optional army groups are accepted");
MpTacticalCheckpoint.Unpack(noGroupsPayload, MpTacticalCheckpoint.Hash(noGroupsPayload), Path.Combine(sandbox, "trailing-directory-separator") + Path.DirectorySeparatorChar);
Check(File.Exists(Path.Combine(sandbox, "trailing-directory-separator", "Battle/scenario.dat")), "A destination with a trailing directory separator still extracts atomically");

Reject(() => MpTacticalCheckpoint.Unpack(packed, "incorrect", Path.Combine(sandbox, "bad-hash")), "Checksum mismatch is rejected before extraction");
Check(!Directory.Exists(Path.Combine(sandbox, "bad-hash")), "Checksum mismatch leaves no save directory");
Reject(() => MpTacticalCheckpoint.Unpack(null, hash, Path.Combine(sandbox, "missing")), "Missing archive is rejected");
Reject(() => MpTacticalCheckpoint.Unpack(Array.Empty<byte>(), hash, Path.Combine(sandbox, "empty")), "Empty archive is rejected");
Reject(() => MpTacticalCheckpoint.Unpack(new byte[MpTacticalCheckpoint.MaxBytes + 1], hash, Path.Combine(sandbox, "compressed-oversize")), "Compressed archive bound is enforced before hashing");
File.WriteAllText(Path.Combine(target, "keep.txt"), "existing save");
Reject(() => MpTacticalCheckpoint.Unpack(packed, hash, target), "Extraction refuses an existing save directory");
Check(File.ReadAllText(Path.Combine(target, "keep.txt")) == "existing save", "Existing saves are never overwritten or cleaned");
var existingFile = Path.Combine(sandbox, "target-file"); File.WriteAllText(existingFile, "keep");
Reject(() => MpTacticalCheckpoint.Unpack(packed, hash, existingFile), "Extraction refuses an existing regular file target");
Check(File.ReadAllText(existingFile) == "keep", "Existing regular file remains intact");
Reject(() => MpTacticalCheckpoint.Unpack(packed, hash, Path.Combine(sandbox, "missing-parent", "new")), "Extraction requires an existing parent directory");

foreach (var badName in new[] { "../escape.dat", "Battle/../../escape.dat", "Battle\\scenario.dat", "/scenario.dat", "C:/scenario.dat", "Battle/GTIP.Runtime.dll", "Battle/nested/scenario.dat", "Battle/scenario.dat:stream", "Battle/dlcwl.dat" })
{
    var entries = Entries(); entries[0] = new KeyValuePair<string, byte[]>(badName, Array.Empty<byte>());
    RejectArchive(Archive(entries), "path-" + checks);
}
var duplicates = Entries(); duplicates[1] = new KeyValuePair<string, byte[]>("SCENARIO.DAT", new byte[] { 1 });
RejectArchive(Archive(duplicates), "case-insensitive-duplicate");
var missing = Entries(); missing.RemoveAll(e => e.Key == "Battle/artillery.dat"); missing.Add(new KeyValuePair<string, byte[]>("armygroups.dat", Array.Empty<byte>()));
RejectArchive(Archive(missing), "required-native-artillery-state-missing");
RejectArchive(Archive(Entries(), firstLength: -1), "negative-size");
RejectArchive(Archive(Entries(), firstLength: MpTacticalCheckpoint.MaxBytes + 1), "inflated-size-over-limit");
RejectArchive(Archive(Entries(), firstLength: MpTacticalCheckpoint.MaxBytes), "truncated-large-declared-file");
RejectArchive(Archive(Entries(), overrideCount: 0), "zero-file-count");
RejectArchive(Archive(Entries(), overrideCount: 48), "excess-file-count");
RejectArchive(Archive(Entries(), trailing: true), "trailing-inflated-data");
RejectArchive(Archive(Entries(), magic: "GTIPBT99"), "unknown-format-version");
var truncated = packed.Take(packed.Length / 2).ToArray();
RejectArchive(truncated, "truncated-gzip-content");

byte[] NameHeader(byte nameLength, byte[] nameBytes)
{
    using var output = new MemoryStream();
    using (var zip = new GZipStream(output, CompressionMode.Compress, true))
    using (var writer = new BinaryWriter(zip, Encoding.UTF8))
    { writer.Write(Encoding.ASCII.GetBytes("GTIPBT01")); writer.Write(required.Length); writer.Write(nameLength); writer.Write(nameBytes); }
    return output.ToArray();
}
RejectArchive(NameHeader(0, Array.Empty<byte>()), "empty-name");
RejectArchive(NameHeader(128, Array.Empty<byte>()), "multibyte-name-prefix");
RejectArchive(NameHeader(100, new byte[] { 1 }), "truncated-name");
RejectArchive(NameHeader(1, new byte[] { 255 }), "invalid-utf8-name");

var unknown = Fixture("unknown-file"); File.WriteAllText(Path.Combine(unknown, "GTIP.Runtime.dll"), "not a save");
Reject(() => MpTacticalCheckpoint.Pack(unknown), "Capture rejects executable files outside the native allowlist");
var extraDirectory = Fixture("unknown-directory"); Directory.CreateDirectory(Path.Combine(extraDirectory, "Other"));
Reject(() => MpTacticalCheckpoint.Pack(extraDirectory), "Capture rejects unexpected directories");
var nested = Fixture("nested-directory"); Directory.CreateDirectory(Path.Combine(nested, "Battle/nested"));
Reject(() => MpTacticalCheckpoint.Pack(nested), "Capture rejects nested directories under Battle");
var incomplete = Fixture("missing-state"); File.Delete(Path.Combine(incomplete, "Battle/sideinfo.dat"));
Reject(() => MpTacticalCheckpoint.Pack(incomplete), "Capture requires the native tactical side-information file");
var tooLarge = Fixture("large-source");
using (var file = File.Create(Path.Combine(tooLarge, "Battle/battleunits.dat"))) file.SetLength(MpTacticalCheckpoint.MaxBytes + 1L);
Reject(() => MpTacticalCheckpoint.Pack(tooLarge), "Capture bounds source file sizes before allocating their payload");
var wrongDirectory = Path.Combine(sandbox, "absent-source");
Reject(() => MpTacticalCheckpoint.Pack(wrongDirectory), "Missing capture source is rejected");

if (args.Length == 1)
{
    var actual = Path.GetFullPath(args[0]);
    var realPacked = MpTacticalCheckpoint.Pack(actual);
    var realTarget = Path.Combine(sandbox, "real-native-save");
    MpTacticalCheckpoint.Unpack(realPacked, MpTacticalCheckpoint.Hash(realPacked), realTarget);
    var files = Directory.GetFiles(actual, "*", SearchOption.AllDirectories);
    var equalBytes = files.All(p => MpTacticalCheckpoint.Hash(File.ReadAllBytes(p)) == MpTacticalCheckpoint.Hash(File.ReadAllBytes(Path.Combine(realTarget, Path.GetRelativePath(actual, p)))));
    Check(files.Length == Directory.GetFiles(realTarget, "*", SearchOption.AllDirectories).Length && equalBytes, "An actual native campaign/battle save round-trips with identical file count and SHA-256 for every file");
    Console.WriteLine("Real native checkpoint compressed bytes: " + realPacked.Length);
}
Check(!Directory.GetDirectories(sandbox, ".gtip-battle-stage-*").Any(), "Rejected archives leave no orphaned extraction stages");
Console.WriteLine("Tactical checkpoint: " + checks + " checks. Fixtures retained at " + sandbox);
