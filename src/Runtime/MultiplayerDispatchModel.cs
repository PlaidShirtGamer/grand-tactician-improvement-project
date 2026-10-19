using System;

namespace GTIP
{
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
