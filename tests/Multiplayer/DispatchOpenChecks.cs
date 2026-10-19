using GTIP;

static class DispatchOpenChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        void Reject(Action action,string name){bool rejected=false;try{action();}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,name);}
        var opens=new MpDispatchOpens();
        opens.Observe(90,0,false);
        Check(opens.Take()==-1,"Historical result messages without a native auto-open event remain closed");
        opens.Observe(91,1,false);
        Check(opens.Take()==91,"A new native battle-result event opens its own telegram");
        Check(opens.Take()==-1,"A native battle-result event is consumed once");
        opens.Observe(91,1,false);
        Check(opens.Take()==-1,"Later read/content deltas cannot reopen a dismissed telegram");
        opens.Observe(92,2,true);
        Check(opens.Take()==-1,"A dispatch locally removed by the guest stays hidden");
        opens.Observe(92,2,false);
        Check(opens.Take()==-1,"An already observed event cannot resurrect a locally removed telegram");
        opens.Observe(94,4,false);opens.Observe(93,3,false);
        Check(opens.Take()==94,"Unordered archive rows preserve the most recent native telegram");
        opens.Observe(96,6,true);opens.Observe(95,5,false);
        Check(opens.Take()==-1,"A newer hidden telegram cannot fall back to an older result");
        opens.Observe(98,8,false);opens.Observe(97,7,true);
        Check(opens.Take()==98,"An older hidden dispatch cannot suppress a newer native telegram");
        Reject(()=>opens.Observe(99,-1,false),"Negative native auto-open serial rejected");
        Reject(()=>opens.Observe(-1,9,false),"Auto-open event requires a dispatch identity");
        opens.Clear();opens.Observe(100,1,false);
        Check(opens.Take()==100,"A new session starts its own battle-result event sequence");
        opens.Observe(101,2,false);opens.Clear();
        Check(opens.Take()==-1,"Disconnect clears a pending dispatch presentation");
        return checks;
    }
}
