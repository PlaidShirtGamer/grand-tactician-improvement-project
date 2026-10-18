using System;
using System.IO;

namespace GTIP
{
    public static class MpConstructionRules
    {
        public static bool Funded(bool armyPlacement,bool ratingOk,bool subsidized,float missingFunding) =>
            armyPlacement || ratingOk || (subsidized && missingFunding>=0);
    }
    public sealed class MpBuildingRecord
    {
        public int Type,Owner,Level,NativeIndex,Garrison;
        public float X,Y,Z,Yaw,Condition;
        public string Name;
        public bool Active;
    }
    public sealed class MpRailroadRecord
    {public int Side;public float Progress,LastUpdate;public bool Permitted;}
    public sealed class MpVesselRecord
    {public int Fleet,Pool,Type,Harbor;public float X,Y,Z,DX,DY,DZ;public string Name;}
    public sealed class MpBuildingDraft
    {public int Type,Unit;public float X,Y,Z;}
    public sealed class MpShipDraft
    {public int Fleet,Type,Count;public string Name;}
    public sealed class MpPathState
    {public int Sample;public int[] Lengths,Status,BlockPath,BlockCorner;public float[] Corners;}
    public sealed class MpSpottedState
    {public string LastSpotted;public float[] Marks;}
    public sealed class MpConstructionSites
    {public int[] Places;}
    public static class MpWorldRules
    {
        public static bool SameBuilding(int type,float x,float z,MpBuildingRecord source) =>
            type==source.Type && Math.Abs(x-source.X)<.1f && Math.Abs(z-source.Z)<.1f;
        public static void UnitOrder(string action,float value,int tier,int readiness,bool pending)
        {
            if(action=="campaign-order") {
                if(Array.IndexOf(new[]{100f,101f,120f,121f,122f,123f},value)<0 || pending || (value==100 && readiness<=0) || (value>=121 && readiness<=1))throw new InvalidDataException("Army order is unavailable under native readiness/order-delay rules.");
            } else if(action=="forced-march") {
                if((value!=0 && value!=1) || (value==1 && readiness<=2))throw new InvalidDataException("Forced march is unavailable under native readiness rules.");
            } else if(action=="fleet-order") {
                if(tier!=17 || value!=(int)value || value<0 || value>3 || (value>0 && readiness<=1))throw new InvalidDataException("Fleet order is unavailable.");
            } else if(action!="unit-stop")throw new InvalidDataException("Unregistered unit order.");
        }
        public static void Building(MpBuildingRecord s,int typeCount,int sides)
        {
            if(s==null || s.Type<0 || s.Type>=typeCount || s.Owner<0 || s.Owner>=sides || s.Level<0 || s.Level>20 || s.NativeIndex<0 || s.NativeIndex>20000 || s.Garrison<0 || s.Name==null || s.Name.Length>500 ||
                !MpRules.Finite(s.X) || !MpRules.Finite(s.Y) || !MpRules.Finite(s.Z) || !MpRules.Finite(s.Yaw) || !MpRules.Finite(s.Condition) || s.Condition<0 || s.Condition>1)throw new InvalidDataException("Invalid building record.");
        }
        public static void Path(MpPathState s)
        {
            if(s.Sample<0 || s.Lengths==null || s.Status==null || s.Lengths.Length!=s.Status.Length || s.Lengths.Length>2000 || s.Corners==null || s.Corners.Length>20000 || s.Corners.Length%3!=0 || s.BlockPath==null || s.BlockCorner==null || s.BlockPath.Length!=s.BlockCorner.Length)throw new InvalidDataException("Invalid native path record.");
            long points=0;foreach(int length in s.Lengths){if(length<0 || length>6666)throw new InvalidDataException("Invalid path segment.");points+=length;}
            if(points*3!=s.Corners.Length || Array.Exists(s.Corners,x=>!MpRules.Finite(x)))throw new InvalidDataException("Invalid path geometry.");
        }
    }
}
