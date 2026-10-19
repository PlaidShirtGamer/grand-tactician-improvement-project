using GTIP;

static class PoseStreamChecks
{
    internal static int Run()
    {
        int checks=0;
        void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
        void Reject(Action action,string label){bool failed=false;try{action();}catch(InvalidDataException){failed=true;}Check(failed,label);}
        bool Near(float a,float b)=>Math.Abs(a-b)<.003f;
        var history=new MpPoseHistory();
        Check(!history.Evaluate(0,false),"Empty motion history has no rendered pose");
        history.Push(100,0,2,4);history.Push(100.1f,10,4,8);
        Check(history.Evaluate(100.05f,false) && Near(history.X,5) && Near(history.Y,3) && Near(history.Z,6),"Host timestamps set interpolation velocity");
        history.Evaluate(101,false);Check(history.X==10,"A delayed next packet holds the received endpoint without extrapolation");
        history.Evaluate(99,false);Check(history.X==0,"Render time before oldest sample holds oldest endpoint");
        Check(!history.Push(99,90,0,0),"Late reliable captures cannot overwrite newer lightweight poses");
        history.Evaluate(101,false);Check(history.X==10,"Rejected old capture leaves pose unchanged");
        history.Evaluate(100.05f,true);Check(history.X==10,"Pause immediately selects final received endpoint");
        history.Push(100.1f,12,4,8);history.Evaluate(101,false);Check(history.X==12,"Equal timestamps replace one sample without dividing by zero");
        history.Push(102,1000,0,0);history.Evaluate(100,false);Check(history.X==1000,"Large authoritative relocation clears old trajectory");
        Reject(()=>history.Push(float.NaN,0,0,0),"Non-finite host timestamp rejected");
        Reject(()=>history.Push(103,float.PositiveInfinity,0,0),"Non-finite motion point rejected");
        Reject(()=>history.Evaluate(float.NaN,false),"Non-finite render timestamp rejected");
        var clock=new MpPoseClock();clock.Observe(100,120);clock.Observe(100.1f,120.2f);
        Check(Near(clock.RenderTime(120.25f),100.1f),"Jitter buffer uses lowest observed clock offset and150ms history");
        clock.Observe(101,120.98f);Check(Near(clock.RenderTime(121.13f),101),"Lower measured transit adjusts the shared time mapping");
        clock.Clear();Check(clock.RenderTime(20)==0,"Disconnect clears clock synchronization");
        Reject(()=>clock.Observe(-1,2),"Negative host clock rejected");
        // At high compression velocity rises, but a fixed source timeline keeps
        // each rendered step proportional instead of restarting a fast tween.
        foreach(float velocity in new[]{1f,50f,500f}) {
            var path=new MpPoseHistory();
            for(int i=0;i<20;i++)path.Push(i*.1f,velocity*i*.1f,0,0);
            path.Evaluate(1.65f,false);float first=path.X;path.Evaluate(1.67f,false);
            Check(Near(path.X-first,velocity*.02f),"High compression follows source velocity: "+velocity);
            path.Evaluate(5,false);Check(Near(path.X,velocity*1.9f),"Buffer starvation never overshoots: "+velocity);
        }
        var frame=new MpMessage{kind="motion",sequence=1,baseRevision=2,date=100,units=new[]{new MpUnit{id=5,parent=0,side=0,tier=3,x=10,y=2,z=7,yaw=90}}};
        MpMotionRules.Validate(frame,2);checks++;
        var decoded=MpCodec.Decode(MpCodec.Encode(frame));MpMotionRules.Validate(decoded,2);
        Check(decoded.units[0].x==10 && decoded.date==100 && decoded.baseRevision==2,"Motion frames survive bounded production wire codec");
        Reject(()=>MpMotionRules.Validate(frame,1),"Motion cannot precede applied lifecycle revision");
        frame.baseRevision=0;Reject(()=>MpMotionRules.Validate(frame,2),"Unacknowledged motion generation rejected");frame.baseRevision=2;
        frame.units=new[]{frame.units[0],frame.units[0]};Reject(()=>MpMotionRules.Validate(frame,2),"Duplicate motion identities rejected");frame.units=new[]{frame.units[0]};
        frame.units[0].x=float.NaN;Reject(()=>MpMotionRules.Validate(frame,2),"Non-finite network motion rejected");frame.units[0].x=10;
        frame.sequence=0;Reject(()=>MpMotionRules.Validate(frame,2),"Motion sequence must be positive");frame.sequence=1;
        frame.date=float.NaN;Reject(()=>MpMotionRules.Validate(frame,2),"Non-finite network source time rejected");frame.date=100;
        var roster=new MpSnapshotRoster();Check(!roster.Contains(5,0,0,3),"Local registration alone cannot authorize a motion identity");
        roster.Acknowledge(1,frame.units);Check(roster.Contains(5,0,0,3),"Receipt authorizes exact motion identity");
        Check(!roster.Contains(5,9,0,3) && !roster.Contains(5,0,1,3) && !roster.Contains(5,0,0,4) && !roster.Contains(6,0,0,3),"Reparented, retyped, other-side and new formations wait for acknowledgment");
        roster.Clear();Check(!roster.Contains(5,0,0,3),"Session reset removes motion eligibility");
        frame.units=Enumerable.Range(1,2000).Select(id=>new MpUnit{id=id,side=0,tier=3,x=id}).ToArray();
        string payload=MpCodec.Encode(frame);MpMotionRules.Validate(MpCodec.Decode(payload),2);
        Check(payload.Length<MpRules.MaxBytes/4,"Two thousand moving formations fit well inside bounded transport frame");
        Console.WriteLine($"Motion history/generation/wire: {checks} checks; 2,000-pose frame {payload.Length:N0} characters. Synthetic sources only.");
        return checks;
    }
}
