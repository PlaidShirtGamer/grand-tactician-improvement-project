using System;
using System.IO;
using System.Text;

namespace GTIP
{
    // Explicit, bounded DTO protocol. Never deserialize game objects or executable types.
    [Serializable] public sealed class MpMessage
    {
        public int protocol = MpRules.Protocol, side, sequence;
        public string kind, token, session, mode, text, campaign;
        public int unit;
        public int baseRevision;
        public string stateHash;
        public float x, z, date;
        public bool paused;
        public byte[] blob;
        public MpClock clock;
        public MpUnit[] units;
        public MpTown[] towns;
    }
    [Serializable] public sealed class MpClock
    {
        public int year, month, day;
        public float hour, elapsed, speed;
        public bool paused;
        public void Validate()
        {
            if(year<1 || year>9999 || month<1 || month>12 || day<1 || day>DateTime.DaysInMonth(year,month) ||
                !MpRules.Finite(hour) || hour<0 || hour>=24 || !MpRules.Finite(elapsed) || !MpRules.Finite(speed) || speed<0)
                throw new InvalidDataException("Invalid host campaign clock.");
        }
    }
    [Serializable] public sealed class MpUnit
    {
        public int id, parent, side, tier, men;
        public string name, commander;
        public float x, y, z, yaw, targetX, targetZ;
        public bool controllable, moving;
    }
    [Serializable] public sealed class MpTown { public string name; public float x, z; }

    public static class MpRules
    {
        public const int Protocol = 17, MaxBytes = 2 * 1024 * 1024;
        public static bool NewClock(int incoming,int current) => incoming>0 && incoming>current;
        public static bool NativeSelectionCollider(string tag,string name,bool paperMap) => name=="UnitNameText" || (name=="UnitSymbol" && paperMap);
        public static void ValidateSnapshot(MpMessage value)
        {
            if (value == null || value.kind != "snapshot") throw new InvalidDataException("Expected snapshot message.");
            if (value.units == null) throw new InvalidDataException("Snapshot units array is missing.");
            if (value.towns == null) throw new InvalidDataException("Snapshot towns array is missing.");
            if (value.units.Length > 20000) throw new InvalidDataException("Snapshot exceeds 20000 units: " + value.units.Length);
            if (value.towns.Length > 10000) throw new InvalidDataException("Snapshot exceeds 10000 towns: " + value.towns.Length);
            if (!Finite(value.date)) throw new InvalidDataException("Snapshot campaign date is not finite.");
            for (int i = 0; i < value.units.Length; i++) {
                var unit = value.units[i];
                if (unit == null) throw new InvalidDataException("Snapshot unit " + i + " is null.");
                if (!Finite(unit.x) || !Finite(unit.y) || !Finite(unit.z) || !Finite(unit.yaw) || !Finite(unit.targetX) || !Finite(unit.targetZ))
                    throw new InvalidDataException("Snapshot unit " + unit.id + " has invalid coordinates.");
            }
            for (int i = 0; i < value.towns.Length; i++) {
                var town = value.towns[i];
                if (town == null) throw new InvalidDataException("Snapshot town " + i + " is null.");
                if (!Finite(town.x) || !Finite(town.z)) throw new InvalidDataException("Snapshot town " + i + " has invalid coordinates.");
            }
        }
        public static int GuestSide(string mode, int hostSide)
        {
            if (hostSide < 0 || hostSide > 1 || (mode != "coop" && mode != "versus"))
                throw new InvalidOperationException("Unsupported faction or mode.");
            return mode == "coop" ? hostSide : 1 - hostSide;
        }
        public static void Authorize(MpMessage message, string session, int side, int unitSide, bool controllable, int lastSequence)
        {
            if (message.protocol != Protocol || message.session != session || message.sequence <= lastSequence || message.sequence <= 0)
                throw new InvalidOperationException("Stale session, protocol or duplicate order.");
            if (message.kind != "move" && message.kind != "stop" && message.kind != "commander" && message.kind!="regroup") throw new InvalidOperationException("Unsupported order.");
            if (side != unitSide || ((message.kind=="move" || message.kind=="stop") && !controllable)) throw new InvalidOperationException("You do not control this formation.");
            if (!Finite(message.x) || !Finite(message.z)) throw new InvalidOperationException("Invalid destination.");
        }
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static void WriteFrame(Stream stream, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            if (bytes.Length == 0 || bytes.Length > MaxBytes) throw new InvalidDataException("Frame too large.");
            var length = new[] { (byte)(bytes.Length >> 24), (byte)(bytes.Length >> 16), (byte)(bytes.Length >> 8), (byte)bytes.Length };
            stream.Write(length, 0, 4); stream.Write(bytes, 0, bytes.Length); stream.Flush();
        }
        public static string ReadFrame(Stream stream)
        {
            var header = ReadExact(stream, 4);
            int size = (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
            if (size <= 0 || size > MaxBytes) throw new InvalidDataException("Invalid frame length.");
            return new UTF8Encoding(false, true).GetString(ReadExact(stream, size));
        }
        static byte[] ReadExact(Stream stream, int count)
        {
            var bytes = new byte[count]; int offset = 0;
            while (offset < count) { int read = stream.Read(bytes, offset, count - offset); if (read == 0) throw new EndOfStreamException(); offset += read; }
            return bytes;
        }
    }
}
