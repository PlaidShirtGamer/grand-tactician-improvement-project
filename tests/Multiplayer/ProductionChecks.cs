using GTIP;

static class ProductionChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        void Reject(Action operation,string message){bool rejected=false;try{operation();}catch(InvalidDataException){rejected=true;}Check(rejected,message);}
        MpProductionSale Sale(int resource,float pcs,float price,int direction=1)=>new(){Resource=resource,Pcs=pcs,Price=price,Direction=direction};
        var caps=new[]{10f,20f,50f,25f,99f,99f};
        var trades=new[]{Sale(0,1,5),Sale(1,3,10),Sale(0,9,5),Sale(2,100,1,0),Sale(3,2,20),Sale(4,1,70),Sale(5,1,80)};
        Check(MpProductionRules.RankSales(6,trades,caps).SequenceEqual(new[]{5,4,0,3}),"Host production ranking filters purchases and retains native sales-revenue order, capped to four icons.");
        Check(MpProductionRules.RankSales(2,new[]{Sale(0,1,30),Sale(1,1,10)},caps).SequenceEqual(new[]{1,0}),"Price clamping and reverse-stable tie order match native trade ranking.");
        Check(MpProductionRules.RankSales(6,null,caps).Length==0,"New building without trades has no production sales icons.");
        Reject(()=>MpProductionRules.RankSales(7,trades,caps),"Invalid resource count rejected.");
        Reject(()=>MpProductionRules.RankSales(6,new[]{Sale(6,1,1)},caps),"Unknown production resource rejected.");
        Reject(()=>MpProductionRules.RankSales(6,new[]{Sale(0,float.NaN,1)},caps),"Invalid sales values rejected.");
        var source=new MpBuildingProduction{ProducedGoods=new[]{1,2,3},RankedGoods=new[]{3,2}};
        MpProductionRules.Production(source,6);checks++;
        var productionSchema=MpStateSchema.Scalars(typeof(MpBuildingProduction));var historySchema=MpStateSchema.Scalars(typeof(MpBuildingHistory));
        var history=new MpBuildingHistory{ProfitValues=new[]{3.1f,4.3f,9.7f},ProfitTime=new[]{1863f,1863.1f,1863.2f},ProfitRhythm=1f/12f,ProfitName="Profit, Dover"};
        MpProductionRules.History(history);checks++;
        var schemas=new[]{productionSchema,historySchema};
        var rows=new[]{new MpStateRow{Kind=0,Id=7,Values=productionSchema.Capture(source)},new MpStateRow{Kind=1,Id=7,Values=historySchema.Capture(history)}};
        var decoded=MpStateCodec.Decode(schemas,MpStateCodec.Encode(schemas,rows));
        var copied=new MpBuildingHistory();historySchema.Apply(copied,decoded[1].Values);
        Check(copied.ProfitValues.SequenceEqual(history.ProfitValues) && copied.ProfitTime.SequenceEqual(history.ProfitTime) && copied.ProfitName==history.ProfitName && copied.ProfitRhythm==history.ProfitRhythm,"Building history dates, exact values, rhythm and names survive snapshot codec.");
        Check(copied.VolumeValues==null && copied.VolumeTime==null && copied.VolumeName==null,"Absent native history remains absent.");
        var baseline=new MpStateBaseline(schemas);var first=baseline.Prepare(1,0,decoded);baseline.Commit(1,first,baseline.Fingerprint(first));
        history.ProfitValues[2]=9.700001f;
        var changes=baseline.Changes(new[]{rows[0],new MpStateRow{Kind=1,Id=7,Values=historySchema.Capture(history)}});
        Check(changes.Length==1 && changes[0].Kind==1,"Small production history change emits an exact delta without rounding.");
        Reject(()=>MpProductionRules.Production(new MpBuildingProduction{ProducedGoods=new[]{1},RankedGoods=new[]{6}},6),"Unknown ranked good rejected.");
        Reject(()=>MpProductionRules.Production(new MpBuildingProduction{ProducedGoods=new[]{1,1},RankedGoods=Array.Empty<int>()},6),"Duplicate produced good rejected.");
        Reject(()=>MpProductionRules.Production(new MpBuildingProduction{ProducedGoods=new[]{1},RankedGoods=new[]{0,1,2,3,4}},6),"Too many production icons rejected.");
        copied.ProfitTime[2]=1862f;Reject(()=>MpProductionRules.History(copied),"Backward company-chart dates rejected.");
        copied.ProfitTime=history.ProfitTime;copied.ProfitRhythm=0;Reject(()=>MpProductionRules.History(copied),"Invalid company-history rhythm rejected.");
        copied.ProfitRhythm=history.ProfitRhythm;copied.ProfitValues=new[]{1f};Reject(()=>MpProductionRules.History(copied),"Mismatched company-history arrays rejected.");
        copied.ProfitValues=new[]{1f,2f,float.PositiveInfinity};Reject(()=>MpProductionRules.History(copied),"Nonfinite company-history value rejected.");
        Console.WriteLine($"Building production: {checks} checks passed for authoritative ranking and native history snapshots.");
        return checks;
    }
}
