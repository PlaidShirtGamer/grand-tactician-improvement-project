using GTIP;
using System.Net;

static class TransportQueueChecks
{
    internal static int Run()
    {
        int checks=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        void Wait(Func<bool> ready,string name){var end=DateTime.UtcNow.AddSeconds(6);bool done=ready();while(!done && DateTime.UtcNow<end){Thread.Sleep(5);done=ready();}Check(done,name);}
        string Take(MpFrameQueue queue){Check(queue.TryTake(out var text),"Expected queued frame");return text;}
        string Clock(int sequence,string session="stall",bool paused=false)=>MpCodec.Encode(new MpMessage{
            kind="clock",session=session,sequence=sequence,
            clock=new MpClock{year=1863,month=2,day=19,hour=20,elapsed=sequence,speed=paused?0:20,paused=paused}});
        Check(!MpRules.ClockDue(true,false,2,1.5f),"Unchanged paused clocks do not send at rendering frequency");
        Check(MpRules.ClockDue(true,false,2,1),"Paused clock retains a one-second heartbeat");
        Check(MpRules.ClockDue(false,false,2,1.9f),"Running clock retains frequent updates");
        Check(MpRules.ClockDue(true,true,2,2),"Pause/speed transitions publish immediately");

        var clocks=new MpFrameQueue(2);
        for(int i=1;i<=1000;i++)if(!clocks.TryAdd(Clock(i)))throw new Exception("Clock flood exceeded bounded queue");
        Check(clocks.Count==1 && clocks.Peak==1 && clocks.Coalesced==999,"A thousand valid clocks occupy one pending slot");
        clocks.TryAdd(Clock(500));Check(MpCodec.Decode(Take(clocks)).sequence==1000,"Stale clock cannot overwrite a newer pending clock");
        var barrier=new MpFrameQueue(8);var chunk=MpCodec.Encode(new MpMessage{kind="data-chunk",session="stall",blob=new byte[32000],sequence=7});
        barrier.TryAdd(Clock(1));barrier.TryAdd(Clock(2));barrier.TryAdd(chunk);barrier.TryAdd(Clock(3));barrier.TryAdd(Clock(4));
        Check(barrier.Count==3 && MpCodec.Decode(Take(barrier)).sequence==2 && Take(barrier)==chunk && MpCodec.Decode(Take(barrier)).sequence==4,"Snapshot chunks remain FIFO barriers between clock runs");
        barrier.TryAdd(Clock(4,"a"));barrier.TryAdd(Clock(5,"b"));Check(barrier.Count==2,"Clock sessions cannot replace each other");
        Take(barrier);Take(barrier);
        var bad=Convert.FromBase64String(Clock(6));bad=bad.Take(bad.Length-1).ToArray();string malformed=Convert.ToBase64String(bad);
        barrier.TryAdd(malformed);barrier.TryAdd(malformed);Check(barrier.Count==2,"Malformed clock envelopes remain ordered for normal rejection");

        var full=new MpFrameQueue(2);full.TryAdd("first");full.TryAdd("second");
        using(var started=new ManualResetEventSlim()) {
            var writer=Task.Run(()=>{started.Set();return full.AddWaiting("third");});started.Wait();
            Check(!writer.Wait(100),"Reliable producer waits when bounded receive queue is full");
            Check(Take(full)=="first","Backpressure preserves the first reliable frame");
            Check(writer.Wait(1000) && writer.Result && full.Peak==2,"Consuming a frame releases producer without growing the queue");
            Check(Take(full)=="second" && Take(full)=="third","Backpressure preserves all reliable frames in order");
        }
        var closing=new MpFrameQueue(1);closing.TryAdd("pending");
        var blocked=Task.Run(()=>closing.AddWaiting("blocked"));
        Check(!blocked.Wait(100),"Close test starts with a blocked producer");closing.Close();
        Check(blocked.Wait(1000) && !blocked.Result,"Disconnect wakes a blocked producer");

        using(var host=MpTransport.Host(IPAddress.Loopback,0))using(var guest=MpTransport.Join(IPAddress.Loopback,host.Port)) {
            Wait(()=>host.Connected && guest.Connected,"Stall test connects through production TCP transport");
            // Reproduce a Unity thread held for longer than the logged 3.6-second
            // initial application. Networking continues; the guest consumes nothing.
            var timer=System.Diagnostics.Stopwatch.StartNew();
            for(int i=1;i<=120;i++){host.Send(Clock(i,paused:i<60));Thread.Sleep(33);}
            host.Send("after-stall");Wait(()=>guest.PendingIncoming>=2 || guest.Closed,"Stalled guest receives clock and ordered barrier");
            Check(timer.Elapsed.TotalSeconds>3.6 && host.Connected && guest.Connected,"More than 64 clocks during a four-second consumer stall do not disconnect");
            Check(guest.Receive(out var latest) && MpCodec.Decode(latest).sequence==120 && !MpCodec.Decode(latest).clock.paused,"Guest resumes with newest authoritative running clock");
            Check(guest.Receive(out var marker) && marker=="after-stall","Reliable message survives the clock flood");
            Check(guest.CoalescedIncoming+host.CoalescedOutgoing>=119 && guest.PeakIncoming<=2,"Clock flood remains bounded on actual network queues");

            var producer=Task.Run(()=> {
                for(int i=0;i<180;i++) {
                    while(host.PendingOutgoing>8 && host.Connected)Thread.Sleep(1);
                    host.Send("reliable "+i);
                }
            });
            Wait(()=>guest.PeakIncoming==64 || guest.Closed,"Reliable traffic fills the configured receive bound");
            Check(guest.Connected && guest.PendingIncoming==64,"Full reliable queue applies backpressure instead of disconnecting");
            for(int i=0;i<180;i++) {
                string text=null;Wait(()=>guest.Receive(out text),"Reliable frame arrives after stalled consumer resumes");
                if(text!="reliable "+i)throw new Exception("Reliable frame lost/reordered at "+i);
            }
            Check(producer.Wait(1000) && host.Connected && guest.Connected && guest.PeakIncoming==64,"All 180 reliable frames survive bounded TCP backpressure");
        }
        Console.WriteLine("Transport regression: four-second consumer stall, 120 clocks, and 180 reliable frames pass with bounded queues. No Unity gameplay exercised.");
        return checks;
    }
}
