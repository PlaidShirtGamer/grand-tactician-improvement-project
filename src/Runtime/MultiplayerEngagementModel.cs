using System.IO;

namespace GTIP
{
    public sealed class MpEngagement
    {
        public int Serial;
        public bool Active;
        public float X,Y,Z;
        public int[] Callers,Commanders,Buttons;
        public string[] Paths,Labels,Totals;
    }
    public sealed class MpEngagementUnit
    {
        public int Side,Order;
        public bool Checked,Editable;
        public string Name,Men,Guns,Delay;
    }
    public sealed class MpFirstDecision
    {
        public int Serial {get;private set;}
        public bool Resolved {get;private set;}
        public void Begin(int serial){if(serial<=Serial)throw new InvalidDataException("Stale engagement.");Serial=serial;Resolved=false;}
        public bool Claim(int serial){if(Serial==0 || serial!=Serial || Resolved)return false;Resolved=true;return true;}
        public void Clear(){Serial=0;Resolved=false;}
    }
}
