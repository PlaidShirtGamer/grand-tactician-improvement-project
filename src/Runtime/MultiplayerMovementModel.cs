using System;
using System.IO;

namespace GTIP
{
    public sealed class MpPathOrder
    {
        readonly System.Collections.Generic.Dictionary<int,int> samples=new System.Collections.Generic.Dictionary<int,int>();
        public bool Accept(int unit,int sample)
        {
            if(unit<=0 || sample<0)throw new InvalidDataException("Invalid path sample.");
            if(samples.TryGetValue(unit,out int prior) && sample<prior)return false;
            samples[unit]=sample;return true;
        }
        public void Clear()=>samples.Clear();
    }
    public sealed class MpMovementView
    {
        public int Unit,Order;
        public bool Minimized;
        public float X,Y,Z,Seconds;
        public int[] Active,Disabled;
    }
    public static class MpMovementRules
    {
        public static void View(MpMovementView view)
        {
            if(view.Unit<=0 || view.Order<=0 || !MpRules.Finite(view.X) || !MpRules.Finite(view.Y) || !MpRules.Finite(view.Z) ||
                !MpRules.Finite(view.Seconds) || view.Seconds<=0 || view.Active==null || view.Disabled==null ||
                view.Active.Length>8 || view.Disabled.Length>8 || Array.Exists(view.Active,x=>x<0 || x>5) || Array.Exists(view.Disabled,x=>x<0 || x>5))
                throw new InvalidDataException("Invalid native movement panel.");
        }
        public static void Ticket(int unit,int order,int currentUnit,int currentOrder)
        {if(unit<=0 || order<=0 || unit!=currentUnit || order!=currentOrder)throw new InvalidOperationException("Movement options expired; issue a new movement order.");}
    }
}
