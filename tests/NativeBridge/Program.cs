using GTIP;
using System.Reflection;

int checks=0;
void Check(bool valid,string name){if(!valid)throw new Exception(name);checks++;}
void Reject<T>(Action action,string name) where T:Exception {try{action();}catch(T){checks++;return;}throw new Exception(name);}
var fixture=new NativeFixture();
Check((string)NativeAccess.Call(fixture,"GetUsedUnit")=="False:0","Native optional unit lookup accepts omitted defaults");
Check((string)NativeAccess.Call(fixture,"GetUsedUnit",true)=="True:0","A supplied first argument preserves the remaining native default");
Check((string)NativeAccess.Call(fixture,"GetUsedUnit",false,3)=="False:3","Explicit native arguments survive unchanged");
Check((int)NativeAccess.Call(typeof(NativeFixture),"StaticDefault")==7,"Static native methods also receive declared defaults");
Reject<TargetParameterCountException>(()=>NativeAccess.Call(fixture,"Required"),"Missing required native arguments must fail");
Reject<TargetParameterCountException>(()=>NativeAccess.Call(fixture,"GetUsedUnit",false,0,1),"Extra native arguments must fail");
Reject<InvalidOperationException>(()=>NativeAccess.Call(fixture,"Fail"),"Native failures preserve their original exception type");
NativeAccess.Call(fixture,"Void");Check(fixture.Calls==1,"Void native operations execute once");
var pause=new MpPanelPause();var log=new object();var options=new object();
Check(pause.Update(log,true,true),"Opening dispatches retains a manual pause");
Check(pause.Update(log,false,true),"Closing dispatches cannot resume a campaign already paused on entry");
pause.Clear();Check(pause.Update(log,true,false),"Opening dispatches holds a running campaign");
Check(!pause.Update(log,false,true),"Closing dispatches restores a campaign that was running on entry");
pause.Clear();pause.Update(log,true,false);pause.Update(options,true,true);
Check(pause.Update(log,false,true),"Closing one of two modal registries cannot resume the other");
Check(!pause.Update(options,false,true),"Last modal closing restores the original running state");
pause.Clear();pause.Update(log,true,false);pause.ManualPause(true);
Check(pause.Update(log,false,true),"An explicit pause during a modal survives its close");
pause.Clear();Check(!pause.Update(log,false,false),"Disconnect clears prior modal ownership");
MpOfficerRules.Promote(5,5,6,0,true);checks++;
Reject<InvalidOperationException>(()=>MpOfficerRules.Promote(6,5,8,0,true),"Duplicate promotions must reject the old rank");
Reject<InvalidOperationException>(()=>MpOfficerRules.Promote(6,6,6,0,true),"Locked maximum rank must reject promotion");
Reject<InvalidOperationException>(()=>MpOfficerRules.Promote(5,5,7,1,true),"Inactive officers cannot be promoted");
Reject<InvalidOperationException>(()=>MpOfficerRules.Promote(5,5,7,0,false),"Career command restrictions remain in force");
Console.WriteLine($"Native bridge and management rules: {checks} checks passed using production sources; no live Unity UI was exercised.");

sealed class NativeFixture
{
    public int Calls;
    private string GetUsedUnit(bool returnHighest=false,int layer=0)=>returnHighest+":"+layer;
    private static int StaticDefault(int value=7)=>value;
    private void Required(int value){}
    private void Fail()=>throw new InvalidOperationException("Native failure");
    private void Void(){Calls++;}
}
