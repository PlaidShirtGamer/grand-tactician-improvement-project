using System;
using System.Collections.Generic;
using System.IO;

namespace GTIP
{
    // Fast events and delayed state snapshots can contain the same notice. Keep a
    // bounded reorder window; an evicted old event can never be replayed later.
    public sealed class MpMapNoticeReceipts
    {
        public const int Window = 2048;
        readonly HashSet<int> seen = new HashSet<int>();
        int highest = -1;
        public int Count => seen.Count;
        public bool Take(int id)
        {
            if(id < 0)throw new InvalidDataException("Invalid map notice identity.");
            if(highest >= Window && id <= highest - Window || !seen.Add(id))return false;
            highest = Math.Max(highest, id);
            if(seen.Count > Window)seen.RemoveWhere(value => value <= highest - Window);
            return true;
        }
        public void Clear(){seen.Clear();highest = -1;}
    }
    public static class MpDispatchSymbols
    {
        public static bool Matches(string symbol, int type, int offset)
        {
            if(symbol == null || symbol.Length != 10 || symbol.Substring(3) != "_Symbol" ||
                !int.TryParse(symbol.Substring(0, 3), out int number))return false;
            return number >= 5 && number <= 46 && (long)type + offset == number;
        }
    }
    // A host result can remain in the replicated archive after either client has
    // closed its telegram. Only a new native auto-open event may display it again.
    public sealed class MpDispatchOpens
    {
        int serial, pending = -1;
        public void Observe(int number, int openSerial, bool hidden)
        {
            if(openSerial < 0 || (openSerial > 0 && number < 0))
                throw new ArgumentOutOfRangeException(nameof(openSerial), "Invalid dispatch auto-open identity.");
            if(openSerial <= serial)return;
            serial = openSerial;
            // Native has one telegram panel. When several results arrive together,
            // the most recently opened one is the result that remains visible.
            pending = hidden ? -1 : number;
        }
        public int Take()
        {
            int number = pending;pending = -1;return number;
        }
        public void Clear(){serial = 0;pending = -1;}
    }
}
