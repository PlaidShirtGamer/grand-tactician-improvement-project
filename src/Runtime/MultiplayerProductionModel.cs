using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GTIP
{
    public sealed class MpBuildingProduction
    {
        public int[] ProducedGoods,RankedGoods;
    }
    public sealed class MpBuildingHistory
    {
        public float[] ProfitValues,ProfitTime,VolumeValues,VolumeTime,TaxValues,TaxTime,EfficiencyValues,EfficiencyTime,WorkforceValues,WorkforceTime;
        public float ProfitRhythm,VolumeRhythm,TaxRhythm,EfficiencyRhythm,WorkforceRhythm;
        public string ProfitName,VolumeName,TaxName,EfficiencyName,WorkforceName;
    }
    public struct MpProductionSale
    {public int Resource,Direction;public float Pcs,Price;}
    public static class MpProductionRules
    {
        // The native production row ranks GetTradeData(..., sell) by revenue.
        // Accumulate each resource in its original trade order, matching native
        // float operations, but scan the history once instead of once per good.
        public static int[] RankSales(int resources,IEnumerable<MpProductionSale> trades,float[] priceCaps)
        {
            if(resources<0 || priceCaps==null || resources>priceCaps.Length)throw new InvalidDataException("Invalid production resource count.");
            var pcs=new float[resources];var prices=new float[resources];
            if(trades!=null)foreach(var trade in trades) {
                if(trade.Direction!=1)continue;
                if(trade.Resource<0 || trade.Resource>=resources || !MpRules.Finite(trade.Pcs) || !MpRules.Finite(trade.Price))throw new InvalidDataException("Invalid production sale.");
                int id=trade.Resource;float oldPcs=pcs[id],oldPrice=prices[id];
                pcs[id]=oldPcs+trade.Pcs;
                prices[id]=(oldPcs*oldPrice+trade.Pcs*trade.Price)/Math.Max(pcs[id],1E-08f);
            }
            return Enumerable.Range(0,resources).Where(i=>pcs[i]!=0)
                .OrderByDescending(i=>Math.Abs(pcs[i])*Math.Min(Math.Max(prices[i],0),priceCaps[i])).ThenByDescending(i=>i).Take(4).ToArray();
        }
        public static void Production(MpBuildingProduction data,int resources)
        {
            if(data==null || data.ProducedGoods==null || data.RankedGoods==null || data.ProducedGoods.Length>resources || data.RankedGoods.Length>4 ||
                data.ProducedGoods.Any(i=>i<0 || i>=resources) || data.RankedGoods.Any(i=>i<0 || i>=resources) ||
                data.ProducedGoods.Distinct().Count()!=data.ProducedGoods.Length || data.RankedGoods.Distinct().Count()!=data.RankedGoods.Length)
                throw new InvalidDataException("Invalid building production record.");
        }
        public static void History(MpBuildingHistory data)
        {
            if(data==null)throw new InvalidDataException("Missing building history.");
            Series(data.ProfitValues,data.ProfitTime,data.ProfitRhythm,data.ProfitName);
            Series(data.VolumeValues,data.VolumeTime,data.VolumeRhythm,data.VolumeName);
            Series(data.TaxValues,data.TaxTime,data.TaxRhythm,data.TaxName);
            Series(data.EfficiencyValues,data.EfficiencyTime,data.EfficiencyRhythm,data.EfficiencyName);
            Series(data.WorkforceValues,data.WorkforceTime,data.WorkforceRhythm,data.WorkforceName);
        }
        static void Series(float[] values,float[] times,float rhythm,string name)
        {
            if(values==null && times==null && rhythm==0 && name==null)return;
            if(values==null || times==null || values.Length!=times.Length || values.Length<2 || values.Length>20000 ||
                !MpRules.Finite(rhythm) || rhythm<=0 || name==null || name.Length>500 ||
                values.Any(v=>!MpRules.Finite(v)) || times.Any(v=>!MpRules.Finite(v)))throw new InvalidDataException("Invalid building history series.");
            for(int i=1;i<times.Length;i++)if(times[i]<times[i-1])throw new InvalidDataException("Building history time moved backwards.");
        }
    }
}
