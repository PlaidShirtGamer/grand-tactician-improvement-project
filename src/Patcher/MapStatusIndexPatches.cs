using Mono.Cecil;
using Mono.Cecil.Cil;

// Extract the game's own status selection, without its UI-existence gate or renderer.
// This keeps the complete native precedence when transient camps are absent on a replica.
internal static class MapStatusIndexPatches
{
    internal const string GetterName="GTIP_NativeMapStatusIndex";

    static (MethodDefinition Source,Instruction[] Selection,Instruction End) Selection(AssemblyDefinition game)
    {
        var type=game.MainModule.Types.Single(t=>t.Name=="Regiment");
        var source=type.Methods.SingleOrDefault(m=>m.Name=="GTIP_Original_ReplicaMapStatusIcon")??type.Methods.Single(m=>m.Name=="UpdateUnitIconTexture");
        if(source.Body.ExceptionHandlers.Count!=0 || source.Parameters.Count!=0 || source.ReturnType.FullName!="System.Void" ||
            source.Body.Variables.Count!=2 || source.Body.Variables[0].VariableType.FullName!="System.Int32")
            throw new InvalidOperationException("Native map status selection layout changed.");
        var code=source.Body.Instructions.ToArray();
        int start=Array.FindIndex(code,i=>i.OpCode==OpCodes.Ldc_I4_0 && i.Next?.OpCode==OpCodes.Stloc_0);
        int firstUi=Array.FindIndex(code,start+1,i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.DeclaringType.Name=="Regiment" && f.Name=="panelhandler");
        int end=firstUi-1;
        if(start<0 || end<=start || code[end].OpCode!=OpCodes.Ldarg_0 ||
            !code.Take(start).Any(i=>i.Operand is FieldReference f && f.Name=="uniticonid") ||
            !code.Skip(end).Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="PanelHandler" && m.Name=="UpdateButtonPic"))
            throw new InvalidOperationException("Native map status UI boundaries changed.");
        var selection=code.Skip(start).Take(end-start).ToArray();
        var allowed=new HashSet<string>{"UnityEngine.Object::op_Inequality","UnityEngine.Object::op_Equality","Regiment::IsLowOnSupply","Regiment::GetSiegeAutocalc"};
        foreach(var i in selection) {
            if(i.OpCode==OpCodes.Stfld || i.OpCode==OpCodes.Stsfld || i.OpCode==OpCodes.Stobj || i.OpCode==OpCodes.Throw || i.OpCode==OpCodes.Ret ||
                i.Operand is MethodReference m && !allowed.Contains(m.DeclaringType.FullName+"::"+m.Name))
                throw new InvalidOperationException("Native map status selection gained an unaudited operation: "+i);
            if(i.Operand is Instruction target && target!=code[end] && !selection.Contains(target))
                throw new InvalidOperationException("Native map status branch escapes the pure selection block.");
        }
        return(source,selection,code[end]);
    }

    internal static void Apply(AssemblyDefinition game)
    {
        var (source,selection,end)=Selection(game);
        if(source.DeclaringType.Methods.Any(m=>m.Name==GetterName))throw new InvalidOperationException("Native map status getter is already installed.");
        var getter=new MethodDefinition(GetterName,MethodAttributes.Public|MethodAttributes.HideBySig,game.MainModule.TypeSystem.Int32);
        getter.Body.InitLocals=source.Body.InitLocals;getter.Body.MaxStackSize=source.Body.MaxStackSize;
        foreach(var v in source.Body.Variables)getter.Body.Variables.Add(new VariableDefinition(v.VariableType));
        var copies=selection.ToDictionary(i=>i,i=>Instruction.Create(OpCodes.Nop));
        var result=Instruction.Create(OpCodes.Ldloc_0);
        Instruction Target(Instruction old)=>old==end?result:copies[old];
        foreach(var old in selection) {
            var copy=copies[old];copy.OpCode=old.OpCode;
            copy.Operand=old.Operand switch {
                Instruction target=>Target(target),
                Instruction[] targets=>targets.Select(Target).ToArray(),
                VariableDefinition v=>getter.Body.Variables[v.Index],
                _=>old.Operand
            };
            getter.Body.Instructions.Add(copy);
        }
        getter.Body.Instructions.Add(result);getter.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        source.DeclaringType.Methods.Add(getter);
        Verify(game);
    }

    internal static void Verify(AssemblyDefinition game)
    {
        var (source,selection,end)=Selection(game);
        var getter=source.DeclaringType.Methods.Single(m=>m.Name==GetterName);
        var actual=getter.Body.Instructions.ToArray();
        if(getter.Parameters.Count!=0 || getter.ReturnType.FullName!="System.Int32" || actual.Length!=selection.Length+2 ||
            actual[actual.Length-2].OpCode!=OpCodes.Ldloc_0 || actual[actual.Length-1].OpCode!=OpCodes.Ret ||
            getter.Body.Variables.Count!=source.Body.Variables.Count)
            throw new InvalidOperationException("Extracted native map status getter shape differs.");
        string Signature(Instruction i,Instruction[] body,Instruction exit)
        {
            string Operand(object value)=>value switch {
                null=>"",Instruction target=>"branch:"+(target==exit?body.Length:Array.IndexOf(body,target)),
                Instruction[] targets=>string.Join(",",targets.Select(t=>Operand(t))),VariableDefinition v=>"local:"+v.Index,
                MethodReference m=>m.FullName,FieldReference f=>f.FullName,_=>value.ToString()??""
            };
            return i.OpCode.Code+" "+Operand(i.Operand);
        }
        var actualSelection=actual.Take(selection.Length).ToArray();
        if(!selection.Select(i=>Signature(i,selection,end)).SequenceEqual(actualSelection.Select(i=>Signature(i,actualSelection,actual[selection.Length]))))
            throw new InvalidOperationException("Extracted map status selection differs from native instructions.");
    }
}
