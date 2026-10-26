using GTIP;
using UnityEngine;

static class NativeStateChecks
{
    public static void Run()
    {
        int checks=0;void Check(bool valid,string reason){if(!valid)throw new Exception(reason);checks++;Console.WriteLine("PASS "+reason);}
        void Reject(Action action,string reason){bool failed=false;try{action();}catch(Exception e)when(e is InvalidDataException || e is InvalidOperationException){failed=true;}Check(failed,reason);}
        (BattleUnits Controller,Regiment Army,Regiment Unit,Regiment Arrival,Objectives Objective) World(bool campaign=false)
        {
            GameVars.gamepaused=GameVars.allregimentsinitialized=true;GameVars.frame=60;GameVars.currenttimefromstart=8;GameVars.playeralliance=0;
            var controller=new GameObject("GameController").Add(new BattleUnits{IsCampaign=campaign});
            BattleUnits.unitfolder=new GameObject("Units");
            Regiment Unit(string name,int type,int commander,Transform parent) {var go=new GameObject(name);go.transform.Parent(parent);var r=go.Add(new Regiment{unittyp=type,commander=commander});var block=new GameObject(name+" block");block.transform.Parent(go.transform);block.transform.position=new(10,1,20);r.blockobject=new[]{block};return r;}
            var army=Unit("II Corps",16,0,BattleUnits.unitfolder.transform);var unit=Unit("5th New Jersey",0,1,army.transform);
            unit.parentregiment=army.gameObject;unit.transform.position=new(10,1,20);unit.transform.eulerAngles=new(0,90,0);
            var arrival=Unit("Reinforcement",0,2,army.transform);arrival.gameObject.activeSelf=false;arrival.initialized=false;arrival.timeofarrival=12;
            GameVars.commander=new(){new(){currentcommand=army},new(){currentcommand=unit},new(){currentcommand=arrival}};
            var objective=new GameObject("Objective1").Add(new Objectives{lastregimenttotake=unit});BattleUnits.objectives=new[]{objective};
            return (controller,army,unit,arrival,objective);
        }
        var a=World();string initial=MpTacticalNativeState.Capture(false);
        Check(Convert.FromBase64String(initial).Length==32,"Production native capture returns a canonical gameplay digest");
        a.Unit.transform.Parent(null);a.Unit.transform.Parent(a.Army.transform);
        Check(initial==MpTacticalNativeState.Capture(false),"Changing scene sibling enumeration leaves the production digest unchanged");
        var b=World();Check(initial==MpTacticalNativeState.Capture(false),"Fresh native object identities representing the same world have the same digest");
        b.Arrival.strength--;Check(initial!=MpTacticalNativeState.Capture(false),"Production capture includes inactive reinforcement state");
        b=World();b.Unit.blockobject![0].transform.position=new(10.01f,1,20);Check(initial!=MpTacticalNativeState.Capture(false),"Production capture reads actual company/block geometry");
        b=World();b.Unit.ammo![0]-=.01f;Check(initial!=MpTacticalNativeState.Capture(false),"Production capture reads native ammunition values");
        b=World();b.Unit.ordertypeactive![0]=true;Check(initial!=MpTacticalNativeState.Capture(false),"Production capture reads loaded order state");
        b=World();b.Unit.blockobjectcurrentcorner![0]++;Check(initial!=MpTacticalNativeState.Capture(false),"Production capture reads formation-section path progress");
        b=World();b.Objective.owner=1;Check(initial!=MpTacticalNativeState.Capture(false),"Production capture reads native objective ownership");
        b=World();GameVars.commander![1].rank++;Check(initial!=MpTacticalNativeState.Capture(false),"Production capture reads officer state independently of unit names");
        b=World();b.Controller.sideinformation![0].totalpoints++;Check(initial!=MpTacticalNativeState.Capture(false),"Production capture reads saved tactical side totals");
        b=World();b.Controller.uniStormSystem!.Hour+=.1f;Check(initial!=MpTacticalNativeState.Capture(false),"Production capture reads the initialized native clock");
        b=World();GameVars.gamepaused=false;Reject(()=>MpTacticalNativeState.Capture(false),"An unpaused native scene cannot attest this proof baseline");
        b=World();GameVars.frame=40;Reject(()=>MpTacticalNativeState.Capture(false),"Early native startup cannot attest readiness");
        b=World();b.Unit.initialized=false;Reject(()=>MpTacticalNativeState.Capture(false),"An active uninitialized unit prevents readiness");
        b=World();b.Unit.parentregiment=new GameObject("broken");Reject(()=>MpTacticalNativeState.Capture(false),"A broken parent-regiment reference cannot be silently treated as no parent");
        b=World();GameVars.commander![0].currentcommand=new GameObject("foreign").Add(new Regiment());Reject(()=>MpTacticalNativeState.Capture(false),"Officer references outside the loaded native world prevent readiness");
        b=World();var duplicate=new GameObject(b.Unit.name);duplicate.transform.Parent(b.Army.transform);duplicate.Add(new Regiment{unittyp=b.Unit.unittyp,alliance=b.Unit.alliance,commander=b.Unit.commander});Reject(()=>MpTacticalNativeState.Capture(false),"Duplicate initialized native identities are rejected");
        b=World();BattleUnits.objectives=null;Reject(()=>MpTacticalNativeState.Capture(false),"Missing tactical objective state prevents readiness");
        b=World(true);BattleUnits.objectives=null;b.Controller.sideinformation=null;Check(Convert.FromBase64String(MpTacticalNativeState.Capture(true)).Length==32,"Campaign return capture does not require absent tactical-only objects");
        Reject(()=>MpTacticalNativeState.Capture(false),"A loaded campaign cannot attest tactical scene readiness");
        Console.WriteLine($"Native capture bridge: {checks} checks with production capture and native object doubles; Unity engine initialization is not exercised.");
    }
}
