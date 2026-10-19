using GTIP;

static class PresentationFenceChecks
{
    sealed class Record {public int Recruits;public string Name;}
    internal static int Run()
    {
        int checks=0;
        void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
        var sequence=new List<string>();var error=new NullReferenceException("native tooltip ancestry");Exception reported=null;
        bool result=MpPresentationFence.Refresh(()=>sequence.Add("draw"),()=>sequence.Add("restore"),e=>reported=e);
        Check(result && sequence.SequenceEqual(new[]{"draw","restore"}) && reported==null,"Successful redraw restores authority without reporting a failure");
        sequence.Clear();result=MpPresentationFence.Refresh(()=>{sequence.Add("draw");throw error;},()=>sequence.Add("restore"),e=>{reported=e;sequence.Add("report");});
        Check(!result && sequence.SequenceEqual(new[]{"draw","restore","report"}) && ReferenceEquals(reported,error),"Tooltip failure is reported only after state restoration");
        bool escaped=false;
        try{MpPresentationFence.Refresh(()=>throw error,()=>throw new InvalidDataException("restoration failed"),e=>throw new Exception("must not report as display-only"));}
        catch(InvalidDataException){escaped=true;}
        Check(escaped,"A native-state restoration error escapes the presentation fence");
        var schema=MpStateSchema.Scalars(typeof(Record));var record=new Record{Recruits=3000,Name="Host unit"};
        var baseline=new MpStateBaseline(new[]{schema});var row=new MpStateRow{Id=1,Values=schema.Capture(record)};
        var rows=baseline.Prepare(1,0,new[]{row});var hash=baseline.Fingerprint(rows);
        MpPresentationFence.Refresh(()=>{record.Recruits=0;record.Name="Partial UI draft";throw error;},()=>schema.Apply(record,row.Values),_=>{});
        Check(MpStateBaseline.FirstDifference(schema,row.Values,schema.Capture(record))==null,"Partially executed native UI callbacks cannot leave registered fields altered");
        baseline.Commit(1,rows,hash);Check(baseline.Revision==1,"Valid state transaction commits despite a redraw exception");
        MpPresentationFence.Refresh(()=>{},()=>{},_=>{});record.Recruits=1;
        Check(MpStateBaseline.FirstDifference(schema,row.Values,schema.Capture(record))=="Recruits","Strict readback still detects real native-state divergence");
        record.Recruits=3000;var next=baseline.Prepare(2,1,Array.Empty<MpStateRow>());baseline.Commit(2,next,baseline.Fingerprint(next));
        Check(baseline.Revision==2,"The next valid transaction remains usable after a display failure");
        return checks;
    }
}
