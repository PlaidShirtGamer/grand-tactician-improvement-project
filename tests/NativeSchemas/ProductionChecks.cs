using System.Reflection;
using System.Collections;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class ProductionChecks
{
    public static void Run(string gamePath,string runtimePath,Array schemas)
    {
        FieldInfo[] Fields(int kind)=>(FieldInfo[])schemas.GetValue(kind)!.GetType().GetField("Fields")!.GetValue(schemas.GetValue(kind))!;
        var building=Fields(18).Select(f=>f.Name).ToHashSet();
        foreach(string field in new[]{"producedpcspa","demandedpcspa","economyalerttooltip","absoluteworkforce","slaveratio","avgprodefficiency"})
            if(!building.Contains(field))throw new Exception("Native building display input omitted: "+field);
        var production=Fields(40).Select(f=>f.Name).ToHashSet();
        if(!production.SetEquals(new[]{"ProducedGoods","RankedGoods"}))throw new Exception("Building sales-ranking schema changed.");
        var history=Fields(41).Select(f=>f.Name).ToHashSet();
        foreach(string prefix in new[]{"Profit","Volume","Tax","Efficiency","Workforce"})foreach(string suffix in new[]{"Values","Time","Rhythm","Name"})
            if(!history.Contains(prefix+suffix))throw new Exception("Native company-history input omitted: "+prefix+suffix);
        using var native=AssemblyDefinition.ReadAssembly(gamePath);
        var type=native.MainModule.Types.Single(t=>t.Name=="Production");
        var histories=new[]{"profitpermonth","volumesold","corporatetax","historicefficiency","historicworkforce"}.ToHashSet();
        var references=new[]{"closesttown"}.ToHashSet();
        var lifecycle=new[]{"level","BuildingType"}.ToHashSet();
        foreach(string method in new[]{"AddIndustryRow","UpdateCompanyInformation"}) {
            var reads=type.Methods.Single(m=>m.Name==method).Body.Instructions
                .Where(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.DeclaringType.Name=="CBuilding")
                .Select(i=>((FieldReference)i.Operand).Name).Distinct().ToArray();
            foreach(string field in reads)
                if(!building.Contains(field) && !histories.Contains(field) && !references.Contains(field) && !lifecycle.Contains(field) && field!="producedgoods" && field!="garrisonunit")
                    throw new Exception("Review new native production row/detail input: "+field);
        }
        using var runtime=AssemblyDefinition.ReadAssembly(runtimePath);
        var adapter=runtime.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign");
        var captures=adapter.Methods.Single(m=>m.Name=="CaptureBuildingHistory").Body.Instructions
            .Where(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.DeclaringType.Name=="CBuilding")
            .Select(i=>((FieldReference)i.Operand).Name).ToHashSet();
        if(!histories.IsSubsetOf(captures))throw new Exception("Building-history adapter omitted a native company dataset.");
        Console.WriteLine("Native production ABI: row and company-detail inputs covered by building values, sales ranks, five histories and lifecycle references.");
        VerifyNativeRankings(gamePath,runtimePath);
    }
    static void VerifyNativeRankings(string gamePath,string runtimePath)
    {
        var game=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(gamePath));
        var runtime=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(runtimePath));
        var buildingType=game.GetType("CBuilding",true)!;var economyType=game.GetType("Economy",true)!;
        var resourceType=game.GetType("Economy+Resource",true)!;var tradeType=game.GetType("IIP+LastTrade",true)!;
        var sourceType=runtime.GetType("GTIP.MpProductionSale",true)!;
        var helper=runtime.GetType("GTIP.MpProductionRules",true)!.GetMethod("RankSales")!;
        var nativeRanking=buildingType.GetMethod("GetHighestTradeIDs")!;
        var resourceField=economyType.GetField("resources")!;var oldResources=resourceField.GetValue(null);
        void Set(Type type,object target,string field,object value)=>type.GetField(field)!.SetValue(target,value);
        var random=new Random(40719);
        try {
            for(int scenario=0;scenario<100;scenario++) {
                const int count=12;
                var caps=Enumerable.Range(0,count).Select(_=>(float)random.Next(1,100)).ToArray();
                var resources=Array.CreateInstance(resourceType,count);
                for(int i=0;i<count;i++) {
                    var resource=RuntimeHelpers.GetUninitializedObject(resourceType);
                    Set(resourceType,resource,"maxprice",caps[i]);Set(resourceType,resource,"minpricereached",1f);resources.SetValue(resource,i);
                }
                resourceField.SetValue(null,resources);
                // These fixtures exercise only managed numerical methods. They do
                // not construct a Unity object, access a scene or invoke a simulation.
                var building=RuntimeHelpers.GetUninitializedObject(buildingType);
                Set(buildingType,building,"stock",new float[count]);
                var trades=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(tradeType))!;
                var sales=Array.CreateInstance(sourceType,100);
                for(int i=0;i<100;i++) {
                    int id=random.Next(count),direction=random.Next(2);float pcs=random.Next(1000)/11f,price=random.Next(-5,150)/3f;
                    var trade=Activator.CreateInstance(tradeType)!;
                    Set(tradeType,trade,"resid",id);Set(tradeType,trade,"buyorsell",direction);Set(tradeType,trade,"pcs",pcs);Set(tradeType,trade,"price",price);trades.Add(trade);
                    var sale=Activator.CreateInstance(sourceType)!;
                    Set(sourceType,sale,"Resource",id);Set(sourceType,sale,"Direction",direction);Set(sourceType,sale,"Pcs",pcs);Set(sourceType,sale,"Price",price);sales.SetValue(sale,i);
                }
                Set(buildingType,building,"historicaltrades",trades);
                var expected=((IEnumerable)nativeRanking.Invoke(building,new object[]{1,4,null!,true})!).Cast<object>().Select(t=>(int)tradeType.GetField("resid")!.GetValue(t)!).ToArray();
                var actual=(int[])helper.Invoke(null,new object[]{count,sales,caps})!;
                if(!expected.SequenceEqual(actual))throw new Exception("Single-pass production ranking differs from native numerical result: "+scenario);
            }
        } finally {resourceField.SetValue(null,oldResources);}
        Console.WriteLine("Native production ranking: 100 synthetic trade histories match the original managed GetHighestTradeIDs result without Unity scene access.");
    }
}
