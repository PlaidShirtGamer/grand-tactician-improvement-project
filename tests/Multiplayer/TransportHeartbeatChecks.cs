using GTIP;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

static class TransportHeartbeatChecks
{
    internal static int Run()
    {
        int checks=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        void Wait(Func<bool> ready,string name,double seconds=6)
        {
            var timer=Stopwatch.StartNew();bool done=ready();
            while(!done && timer.Elapsed.TotalSeconds<seconds){Thread.Sleep(5);done=ready();}
            Check(done,name);
        }
        string Take(MpTransport transport)
        {
            string text=null;Wait(()=>transport.Receive(out text),"Expected reliable application frame");return text;
        }

        using var host=MpTransport.Host(IPAddress.Loopback,0);
        using var guest=MpTransport.Join(IPAddress.Loopback,host.Port);
        using var silentHost=MpTransport.Host(IPAddress.Loopback,0);
        using var silentPeer=new TcpClient();
        silentPeer.Connect(IPAddress.Loopback,silentHost.Port);
        Wait(()=>host.Connected && guest.Connected && silentHost.Connected,"Heartbeat tests connect through production TCP transport");

        // Neither application sends or receives during this interval. Unity can
        // block both main threads while loading; only transport workers run.
        var idle=Stopwatch.StartNew();
        while(idle.Elapsed.TotalSeconds<18)Thread.Sleep(20);
        Check(idle.Elapsed.TotalSeconds>15 && host.Connected && guest.Connected,
            "An eighteen-second application stall survives the original fifteen-second receive timeout");
        Check(host.HeartbeatsSent>=10 && guest.HeartbeatsSent>=10 && host.HeartbeatsReceived>=10 && guest.HeartbeatsReceived>=10,
            "Both socket workers exchange liveness while application callers are idle");
        Check(host.PendingIncoming==0 && guest.PendingIncoming==0 && host.PeakIncoming==0 && guest.PeakIncoming==0,
            "Worker heartbeats never consume bounded application queue slots");
        Wait(()=>silentHost.Closed,"A genuinely silent raw peer still reaches its receive timeout",5);
        Check(!string.IsNullOrEmpty(silentHost.Failure) && silentHost.Failure.Contains("IOException"),
            "Local heartbeat sends do not conceal the remote peer's missing traffic");

        string session="loading-regression";
        string ready=MpCodec.Encode(new MpMessage{kind="ready",session=session});
        string chunk=MpCodec.Encode(new MpMessage{kind="checkpoint-data",session=session,sequence=7,blob=new byte[]{1,2,3,4}});
        string welcome=MpCodec.Encode(new MpMessage{kind="welcome",session=session});
        guest.Send(ready);host.Send(chunk);host.Send(welcome);
        Check(Take(host)==ready,"Readiness is delivered after the main thread resumes");
        Check(Take(guest)==chunk && Take(guest)==welcome,"Reliable checkpoint and handshake frames retain FIFO order across the load stall");

        string exact=MpCodec.Encode(new MpMessage{kind="transport-heartbeat"});
        string altered=MpCodec.Encode(new MpMessage{kind="transport-heartbeat",session=session,text="application content"});
        byte[] oldVersion=Convert.FromBase64String(exact);oldVersion[0]++;
        string wrongProtocol=Convert.ToBase64String(oldVersion);
        int receivedBefore=host.HeartbeatsReceived;
        guest.Send(exact);guest.Send(altered);guest.Send(wrongProtocol);guest.Send(ready);
        Check(Take(host)==altered && Take(host)==wrongProtocol && Take(host)==ready,
            "Only the byte-exact heartbeat is filtered; modified and wrong-version frames retain validation order");
        Wait(()=>host.HeartbeatsReceived>receivedBefore,"An exact heartbeat increments the transport diagnostic counter");
        bool rejected=false;
        try{MpCodec.Decode(wrongProtocol);}catch(InvalidDataException){rejected=true;}
        Check(rejected,"Normal protocol validation still rejects a forwarded wrong-version heartbeat");

        guest.Dispose();
        Wait(()=>host.Closed,"A closed peer is detected promptly despite prior successful heartbeats");
        Check(host.Failure!=null && host.Failure.Contains("EndOfStreamException"),"Peer EOF remains a concrete disconnect reason");
        Console.WriteLine("Transport heartbeat regression: eighteen-second idle applications stay connected; silent/closed peers still disconnect and modified frames retain normal validation. No Unity gameplay exercised.");
        return checks;
    }
}
