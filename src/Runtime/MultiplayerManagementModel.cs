using System;
using System.IO;

namespace GTIP
{
    public sealed class MpProjectState { public int[] Projects,LastMonth; }
    public sealed class MpNewspaperState
    {
        public int Serial,Image,Day,Month,Year;
        public bool Active,HostClosed;
        public string[] Headlines;
        public string Prosa,ImageHeadline,Location;
    }
    // A new article is its own barrier: acknowledgements cannot dismiss a later issue.
    public sealed class MpNewsBarrier
    {
        public int Serial {get;private set;}
        public bool HostClosed {get;private set;}
        public bool GuestClosed {get;private set;}
        public bool Waiting => Serial>0 && !(HostClosed && GuestClosed);
        public void Begin(int serial) {if(serial<=Serial)throw new InvalidDataException("Stale newspaper issue.");Serial=serial;HostClosed=GuestClosed=false;}
        public bool Close(int serial,bool host) {if(serial!=Serial || Serial==0)return false;if(host)HostClosed=true;else GuestClosed=true;return !Waiting;}
        public void Clear(){Serial=0;HostClosed=GuestClosed=false;}
    }
    public sealed class MpRecruitDraft
    {
        public int Parent,State,Type,SizeLevel,RecruitType;
        public bool Horse;
        public void Validate()
        {
            if(Parent<=0 || State<0 || State>1000 || Type<0 || Type>2 || SizeLevel<0 || SizeLevel>2 || RecruitType<0 || RecruitType>1 || (Horse && Type!=2))
                throw new InvalidDataException("Invalid recruitment draft.");
        }
    }
}
