using System;
using System.IO;
using System.Text;

namespace GTIP
{
    // Shared by the Unity runtime and transport tests. No Unity-specific serialization.
    public static class MpCodec
    {
        const int MaxStringBytes = 4096;
        static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        public static string Encode(MpMessage message)
        {
            if (message == null) throw new InvalidDataException("Missing message.");
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream, Utf8)) {
                writer.Write(MpRules.Protocol); Write(writer, message.kind);
                Write(writer, message.token); Write(writer, message.session); Write(writer, message.mode); Write(writer, message.text);
                Write(writer, message.campaign);
                writer.Write(message.side); writer.Write(message.sequence); writer.Write(message.unit);
                writer.Write(message.x); writer.Write(message.z); writer.Write(message.date); writer.Write(message.paused);
                var blob=message.blob??new byte[0];
                Count(blob.Length,MpCheckpoint.ChunkBytes,"checkpoint chunk"); writer.Write(blob.Length); writer.Write(blob);
                writer.Write(message.clock!=null);
                if(message.clock!=null) {
                    message.clock.Validate(); var c=message.clock;
                    writer.Write(c.year); writer.Write(c.month); writer.Write(c.day); writer.Write(c.hour);
                    writer.Write(c.elapsed); writer.Write(c.speed); writer.Write(c.paused);
                }
                var units = message.units ?? new MpUnit[0]; var towns = message.towns ?? new MpTown[0];
                Count(units.Length, 20000, "units"); Count(towns.Length, 10000, "towns");
                writer.Write(units.Length);
                foreach (var unit in units) {
                    if (unit == null) throw new InvalidDataException("Null unit record.");
                    writer.Write(unit.id); writer.Write(unit.parent); writer.Write(unit.side); writer.Write(unit.tier); writer.Write(unit.men);
                    Write(writer, unit.name); Write(writer, unit.commander);
                    writer.Write(unit.x); writer.Write(unit.y); writer.Write(unit.z); writer.Write(unit.yaw); writer.Write(unit.targetX); writer.Write(unit.targetZ);
                    writer.Write(unit.controllable); writer.Write(unit.moving);
                    Bound(stream);
                }
                writer.Write(towns.Length);
                foreach (var town in towns) {
                    if (town == null) throw new InvalidDataException("Null town record.");
                    Write(writer, town.name); writer.Write(town.x); writer.Write(town.z); Bound(stream);
                }
                var result = Convert.ToBase64String(stream.ToArray());
                if (result.Length > MpRules.MaxBytes) throw new InvalidDataException("Encoded message exceeds frame limit.");
                return result;
            }
        }
        public static MpMessage Decode(string encoded)
        {
            if (string.IsNullOrEmpty(encoded) || encoded.Length > MpRules.MaxBytes) throw new InvalidDataException("Invalid encoded message length.");
            using (var stream = new MemoryStream(Convert.FromBase64String(encoded))) using (var reader = new BinaryReader(stream, Utf8)) {
                int protocol = reader.ReadInt32();
                if (protocol != MpRules.Protocol) throw new InvalidDataException("Multiplayer protocol mismatch. Update both installs.");
                var result = new MpMessage { protocol = protocol, kind = Read(reader), token = Read(reader), session = Read(reader), mode = Read(reader), text = Read(reader), campaign = Read(reader),
                    side = reader.ReadInt32(), sequence = reader.ReadInt32(), unit = reader.ReadInt32(), x = reader.ReadSingle(), z = reader.ReadSingle(), date = reader.ReadSingle(), paused = reader.ReadBoolean() };
                int blobSize=Count(reader.ReadInt32(),MpCheckpoint.ChunkBytes,"checkpoint chunk");
                result.blob=reader.ReadBytes(blobSize);
                if(result.blob.Length!=blobSize) throw new EndOfStreamException("Incomplete checkpoint chunk.");
                if(reader.ReadBoolean()) {
                    result.clock=new MpClock { year=reader.ReadInt32(), month=reader.ReadInt32(), day=reader.ReadInt32(), hour=reader.ReadSingle(), elapsed=reader.ReadSingle(), speed=reader.ReadSingle(), paused=reader.ReadBoolean() };
                    result.clock.Validate();
                }
                result.units = new MpUnit[Count(reader.ReadInt32(), 20000, "units")];
                for (int i = 0; i < result.units.Length; i++) result.units[i] = new MpUnit {
                    id = reader.ReadInt32(), parent = reader.ReadInt32(), side = reader.ReadInt32(), tier = reader.ReadInt32(), men = reader.ReadInt32(),
                    name = Read(reader), commander = Read(reader), x = reader.ReadSingle(), y = reader.ReadSingle(), z = reader.ReadSingle(), yaw = reader.ReadSingle(), targetX = reader.ReadSingle(), targetZ = reader.ReadSingle(),
                    controllable = reader.ReadBoolean(), moving = reader.ReadBoolean() };
                result.towns = new MpTown[Count(reader.ReadInt32(), 10000, "towns")];
                for (int i = 0; i < result.towns.Length; i++) result.towns[i] = new MpTown { name = Read(reader), x = reader.ReadSingle(), z = reader.ReadSingle() };
                if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected trailing message data.");
                return result;
            }
        }
        static int Count(int value, int max, string field)
        { if (value < 0 || value > max) throw new InvalidDataException("Invalid " + field + " count: " + value + " (limit " + max + ")."); return value; }
        static void Bound(Stream stream) { if (stream.Length > MpRules.MaxBytes * 3 / 4) throw new InvalidDataException("Message exceeds frame limit."); }
        static void Write(BinaryWriter writer, string value)
        {
            var bytes = Utf8.GetBytes(value ?? "");
            if (bytes.Length > MaxStringBytes) throw new InvalidDataException("Message string exceeds 4096 bytes.");
            writer.Write(bytes.Length); writer.Write(bytes);
        }
        static string Read(BinaryReader reader)
        {
            int count = Count(reader.ReadInt32(), MaxStringBytes, "string byte");
            var bytes = reader.ReadBytes(count);
            if (bytes.Length != count) throw new EndOfStreamException("Truncated string.");
            return Utf8.GetString(bytes);
        }
    }
}
