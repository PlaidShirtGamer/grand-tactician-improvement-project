using System.Reflection;

static class SnapshotChecks
{
    public static void Run(Assembly runtime,Array schemas)
    {
        var schemaType=runtime.GetType("GTIP.MpStateSchema",true)!;
        var rowType=runtime.GetType("GTIP.MpStateRow",true)!;
        var codec=runtime.GetType("GTIP.MpStateCodec",true)!;
        var baselineType=runtime.GetType("GTIP.MpStateBaseline",true)!;
        FieldInfo[] Fields(int kind)=>(FieldInfo[])schemaType.GetField("Fields")!.GetValue(schemas.GetValue(kind))!;
        var nan=(HashSet<string>)schemaType.GetField("NaNFields")!.GetValue(schemas.GetValue(3))!;
        var infinity=(HashSet<string>)schemaType.GetField("InfinityFields")!.GetValue(schemas.GetValue(3))!;
        if(!nan.SetEquals(new[]{"fullreadinessreached"}) || !infinity.SetEquals(new[]{"fullreadinessreached"}))throw new Exception("Native unit special-float policy must be limited to the readiness ETA.");
        foreach(var schema in schemas.Cast<object>().Where((_,kind)=>kind!=3))
            if(((HashSet<string>)schemaType.GetField("InfinityFields")!.GetValue(schema)!).Count!=0)throw new Exception("Unreviewed infinity policy in native schema.");
        object Value(Type type)=>type==typeof(string)?"fixture":type.IsArray?Array.CreateInstance(type.GetElementType()!,Enumerable.Repeat(2,type.GetArrayRank()).ToArray()):Activator.CreateInstance(type)!;
        object Row(int kind,int id,float? estimate=null)
        {
            var fields=Fields(kind);var values=fields.Select(f=>Value(f.FieldType)).ToArray();
            if(estimate.HasValue)values[Array.FindIndex(fields,f=>f.Name=="fullreadinessreached")]=estimate.Value;
            var row=Activator.CreateInstance(rowType)!;
            rowType.GetField("Kind")!.SetValue(row,kind);rowType.GetField("Id")!.SetValue(row,id);rowType.GetField("Name")!.SetValue(row,"snapshot fixture");rowType.GetField("Values")!.SetValue(row,values);return row;
        }
        // Actual compiled game schemas and production codec/baseline, with synthetic
        // field values. No Unity object or native simulation is constructed or run.
        var values=Enumerable.Range(0,schemas.Length).Select(kind=>Row(kind,0)).ToList();
        var estimates=new[]{0f,.25f,float.PositiveInfinity,float.NegativeInfinity,float.NaN};
        for(int i=0;i<estimates.Length;i++)values.Add(Row(3,i+1,estimates[i]));
        var rows=Array.CreateInstance(rowType,values.Count);for(int i=0;i<values.Count;i++)rows.SetValue(values[i],i);
        object MakeBaseline()=>Activator.CreateInstance(baselineType,new object[]{schemas})!;
        var host=MakeBaseline();var guest=MakeBaseline();
        var prepare=baselineType.GetMethod("Prepare")!;var fingerprint=baselineType.GetMethod("Fingerprint")!;var commit=baselineType.GetMethod("Commit")!;
        var complete=prepare.Invoke(host,new object[]{1,0,rows,null})!;
        var hash=(string)fingerprint.Invoke(host,new[]{complete})!;
        var packet=codec.GetMethod("Encode")!.Invoke(null,new object[]{schemas,complete,true})!;
        var decoded=codec.GetMethod("Decode")!.Invoke(null,new[]{schemas,packet})!;
        var received=(Array)prepare.Invoke(guest,new object[]{1,0,decoded,hash})!;
        commit.Invoke(host,new object[]{1,complete,hash});commit.Invoke(guest,new object[]{1,received,hash});
        int etaIndex=Array.FindIndex(Fields(3),f=>f.Name=="fullreadinessreached");
        foreach(var row in received.Cast<object>().Where(row=>(int)rowType.GetField("Kind")!.GetValue(row)! ==3 && (int)rowType.GetField("Id")!.GetValue(row)! >0)) {
            int id=(int)rowType.GetField("Id")!.GetValue(row)!;var data=(object[])rowType.GetField("Values")!.GetValue(row)!;
            if(!((float)data[etaIndex]).Equals(estimates[id-1]))throw new Exception("Native-schema readiness value changed during snapshot reconstruction.");
        }
        if(!Equals(baselineType.GetProperty("Hash")!.GetValue(host),baselineType.GetProperty("Hash")!.GetValue(guest)))throw new Exception("Native-schema full-state checksums diverged.");
        Console.WriteLine($"Native snapshot: {schemas.Length} compiled schemas / {values.Count} synthetic records pass encode, decode, reconstruction and commit, including infinite/NaN readiness estimates. No live Unity state exercised.");
    }
}
