using System;
using System.IO;

namespace GTIP
{
    public static class MpPresentationRules
    {
        public static bool ResumeNewspaper(bool canResume,bool stayPaused,bool otherPanel,float storedSpeed) =>
            canResume && !stayPaused && !otherPanel && MpRules.Finite(storedSpeed) && storedSpeed>0;
        public static void ValidateChart(MpChartPoints s)
        {
            if(s.Time==null || s.Value==null || s.Time.Length!=s.Value.Length || s.Time.Length>20000 ||
                Array.Exists(s.Time,x=>!MpRules.Finite(x)) || Array.Exists(s.Value,x=>!MpRules.Finite(x)))throw new InvalidDataException("Invalid goods chart points.");
            for(int i=1;i<s.Time.Length;i++)if(s.Time[i]<s.Time[i-1])throw new InvalidDataException("Goods chart time moved backwards.");
        }
    }
    public sealed class MpMapNotice
    {
        public int Type,Reference;
        public float X,Y,Z,Age;
        public string Text;
        public bool Icon;
    }
    public sealed class MpChartPoints {public float[] Time,Value;}
    public sealed class MpCampaignControls {public bool AutoFinances,AutoGovernment,AutoCompanies,AutoRailroads,AutoPlace,AutoWeapons,UseFog;public int Chapter;}
    public sealed class MpAlertState {public int Count;public bool Blink;public string Headline,Tooltip;public float[] Positions;}
    public static class MpFinanceRules
    {
        public static float Normalize(int category,int index,float value,int taxes,int subsidies,int minPolicies,int maxPolicies)
        {
            if(!MpRules.Finite(value) || value<0 || value>1 || category<0 || category>2 || index<0 ||
                (category==0 && index>=taxes) || (category==1 && index>=subsidies) || (category==2 && (index!=0 || (value!=0 && value!=1))))throw new InvalidDataException("Invalid financial setting.");
            if(category==1 && index==0){int steps=Math.Max(1,maxPolicies-minPolicies);return (float)Math.Round(value*steps)/steps;}
            return value;
        }
    }
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
        public int Commander=-1,Uniform;
        public string Name="";
        public float[] Colors;
        public bool Horse;
        public void Validate()
        {
            if(Parent<=0 || State<0 || State>1000 || Type<0 || Type>2 || SizeLevel<0 || SizeLevel>2 || RecruitType<0 || RecruitType>1 || (Horse && Type!=2) || Commander< -1 || Uniform<0 || Uniform>20 || Name==null || Name.Length>100 || Name.IndexOfAny(new[]{'\r','\n','\0'})>=0 || (Colors!=null && (Colors.Length!=8 || Array.Exists(Colors,c=>!MpRules.Finite(c)||c<0||c>1))))
                throw new InvalidDataException("Invalid recruitment draft.");
        }
    }
}
