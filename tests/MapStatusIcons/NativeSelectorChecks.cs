using Mono.Cecil;
using Mono.Cecil.Cil;

static class NativeSelectorChecks
{
    sealed class Fields:Dictionary<string,object> {}
    public static void Run(string path,Action<bool,string> check,string managed)
    {
        using var resolver=new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));resolver.AddSearchDirectory(managed);
        using var game=AssemblyDefinition.ReadAssembly(path,new ReaderParameters{AssemblyResolver=resolver});
        var regiment=game.MainModule.Types.Single(t=>t.Name=="Regiment");
        var source=regiment.Methods.Single(m=>m.Name=="UpdateUnitIconTexture");
        MapStatusIndexPatches.Apply(game);MapStatusIndexPatches.Verify(game);
        var getter=regiment.Methods.Single(m=>m.Name==MapStatusIndexPatches.GetterName);
        check(getter.Body.Instructions.All(i=>i.OpCode!=OpCodes.Stfld && i.OpCode!=OpCodes.Stsfld),"Extracted native status selection has no native field writes");
        check(!getter.Body.Instructions.Any(i=>i.Operand is FieldReference f && new[]{"panelhandler","uniticonid"}.Contains(f.Name)),"Host status selection is independent of native UI existence and camera visibility");
        int Evaluate(Fields unit)=>Interpret(getter.Body.Instructions.ToArray(),unit);
        Fields Unit()=>new(){["unittyp"]=14,["currentmodeltype"]=-1,["campaignstance"]=0,["cavalryorders"]=0,["shownmodel"]=null,["campbuilding"]=null,
            ["entrenchment"]=null,["entrenchmentlevel"]=0f,["doublequick"]=false,["garrisonreference"]=null,["lowSupply"]=false,["inbattle"]=false,["fleetorders"]=0,["siege"]=null};
        var unit=Unit();check(Evaluate(unit)==17,"Native offensive stance selects the side-chevron status");
        unit["campaignstance"]=1;check(Evaluate(unit)==18,"Native defensive stance selects its normal stance status without entrenchment");
        unit["entrenchmentlevel"]=.8f;check(Evaluate(unit)==18,"Entrenchment level alone reproduces the guest reference-gap chevron");
        unit["entrenchment"]=new Fields();unit["entrenchmentlevel"]=.15f;check(Evaluate(unit)==23,"Native entrenchment level one overrides stance with the first crown");
        unit["entrenchmentlevel"]=.4f;check(Evaluate(unit)==24,"Native entrenchment level two retains its exact threshold precedence");
        unit["entrenchmentlevel"]=.7f;check(Evaluate(unit)==25,"Native entrenchment level three selects the full crown");
        unit["doublequick"]=true;check(Evaluate(unit)==4,"Native forced march overrides entrenchment");
        unit["garrisonreference"]=new Fields();check(Evaluate(unit)==16,"Native garrison status overrides forced march");
        unit["lowSupply"]=true;check(Evaluate(unit)==28,"Native low supply overrides garrison and entrenchment");
        unit["inbattle"]=true;check(Evaluate(unit)==11,"Native battle status overrides all earlier statuses");
        unit["siege"]=new Fields{["type"]=2};check(Evaluate(unit)==27,"Native siege status retains the final battle override");
        unit=Unit();unit["campbuilding"]=new Fields{["BuildingType"]=24};check(Evaluate(unit)==5,"Native summer camp status is preserved");
        unit["campbuilding"]=new Fields{["BuildingType"]=25};check(Evaluate(unit)==6,"Native winter quarters status is preserved");
        for(int type=0;type<3;type++){unit=Unit();unit["shownmodel"]=new Fields();unit["currentmodeltype"]=type;check(Evaluate(unit)==type+1,"Native transport model status preserved: "+type);}
        unit=Unit();unit["cavalryorders"]=1;check(Evaluate(unit)==13,"Native scouting status preserved");
        unit["cavalryorders"]=2;check(Evaluate(unit)==12,"Native raiding status preserved");
        unit["cavalryorders"]=3;check(Evaluate(unit)==19,"Native cavalry guard status preserved");
        for(int order=1;order<=3;order++){unit=Unit();unit["unittyp"]=17;unit["fleetorders"]=order;check(Evaluate(unit)==new[]{15,14,26}[order-1],"Native fleet order status preserved: "+order);}

        // Verify after the shared patcher wraps the original renderer, and after
        // serialization, so metadata branches/locals are checked in the staged form.
        source.Name="GTIP_Original_ReplicaMapStatusIcon";
        var wrapper=new MethodDefinition("UpdateUnitIconTexture",source.Attributes,source.ReturnType);wrapper.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));regiment.Methods.Add(wrapper);
        MapStatusIndexPatches.Verify(game);
        using(var bytes=new MemoryStream()){game.Write(bytes);bytes.Position=0;using var roundtrip=AssemblyDefinition.ReadAssembly(bytes);MapStatusIndexPatches.Verify(roundtrip);}
        check(true,"Extracted native status branches retain parity after wrapping and assembly serialization");
        var changed=getter.Body.Instructions.First(i=>i.OpCode==OpCodes.Ldc_I4_S && Convert.ToInt32(i.Operand)==23);
        var old=changed.Operand;changed.Operand=(sbyte)17;bool rejected=false;
        try{MapStatusIndexPatches.Verify(game);}catch(InvalidOperationException){rejected=true;}
        changed.Operand=old;check(rejected,"Patcher verification rejects a crown-to-chevron change in extracted selector metadata");
    }

    static int Interpret(Instruction[] code,Fields unit)
    {
        var stack=new Stack<object>();var locals=new object[2];int pc=0;
        var statics=new Fields{["id_summercamp"]=24,["id_winterquarters"]=25,["entrenchmentmodellevel1"]=.15f,["entrenchmentmodellevel2"]=.4f,["entrenchmentmodellevel3"]=.7f};
        bool True(object value)=>value!=null && (value is bool b?b:value is Fields || Convert.ToDouble(value)!=0);
        bool Equal(object left,object right)=>left==null || right==null?ReferenceEquals(left,right):left is Fields || right is Fields?ReferenceEquals(left,right):Convert.ToDouble(left)==Convert.ToDouble(right);
        int Target(Instruction i)=>Array.IndexOf(code,i);
        for(int steps=0;steps<1000;steps++) {
            var i=code[pc++];var op=i.OpCode.Code;
            if(op>=Code.Ldc_I4_0 && op<=Code.Ldc_I4_8){stack.Push((int)op-(int)Code.Ldc_I4_0);continue;}
            switch(op) {
                case Code.Ldarg_0:stack.Push(unit);break;
                case Code.Ldnull:stack.Push(null);break;
                case Code.Ldc_I4_S:case Code.Ldc_I4:stack.Push(Convert.ToInt32(i.Operand));break;
                case Code.Ldfld:stack.Push(((Fields)stack.Pop())[((FieldReference)i.Operand).Name]);break;
                case Code.Ldsfld:stack.Push(statics[((FieldReference)i.Operand).Name]);break;
                case Code.Stloc_0:locals[0]=stack.Pop();break;case Code.Stloc_1:locals[1]=stack.Pop();break;
                case Code.Ldloc_0:stack.Push(locals[0]);break;case Code.Ldloc_1:stack.Push(locals[1]);break;
                case Code.Br:case Code.Br_S:pc=Target((Instruction)i.Operand);break;
                case Code.Brtrue:case Code.Brtrue_S:if(True(stack.Pop()))pc=Target((Instruction)i.Operand);break;
                case Code.Brfalse:case Code.Brfalse_S:if(!True(stack.Pop()))pc=Target((Instruction)i.Operand);break;
                case Code.Beq:case Code.Beq_S:{var right=stack.Pop();if(Equal(stack.Pop(),right))pc=Target((Instruction)i.Operand);break;}
                case Code.Bne_Un:case Code.Bne_Un_S:{var right=stack.Pop();if(!Equal(stack.Pop(),right))pc=Target((Instruction)i.Operand);break;}
                case Code.Blt_Un:case Code.Blt_Un_S:{double right=Convert.ToDouble(stack.Pop()),left=Convert.ToDouble(stack.Pop());if(double.IsNaN(left)||double.IsNaN(right)||left<right)pc=Target((Instruction)i.Operand);break;}
                case Code.Call:{
                    var method=(MethodReference)i.Operand;
                    if(method.Name=="op_Inequality" || method.Name=="op_Equality"){var right=stack.Pop();bool equal=Equal(stack.Pop(),right);stack.Push(method.Name=="op_Equality"?equal:!equal);}
                    else {var receiver=(Fields)stack.Pop();stack.Push(receiver[method.Name=="IsLowOnSupply"?"lowSupply":method.Name=="GetSiegeAutocalc"?"siege":throw new Exception("Unrecognized native status call: "+method.FullName)]);}
                    break;
                }
                case Code.Ret:return Convert.ToInt32(stack.Pop());
                default:throw new Exception("Unrecognized native status opcode: "+i);
            }
        }
        throw new Exception("Native status interpreter exceeded branch limit.");
    }
}
