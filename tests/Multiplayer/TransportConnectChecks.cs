using GTIP;
using System.Diagnostics;
using System.Net;

static class TransportConnectChecks
{
    internal static int Run()
    {
        int checks=0;
        // Repeated fast completion used to race disposal of the APM wait handle.
        for(int i=0;i<24;i++) {
            using var host=MpTransport.Host(IPAddress.Loopback,0);
            using var guest=MpTransport.Join(IPAddress.Loopback,host.Port);
            var timer=Stopwatch.StartNew();
            while(!host.Connected || !guest.Connected) {
                if(host.Closed || guest.Closed || timer.Elapsed.TotalSeconds>6)
                    throw new Exception("Rapid TCP join failed: "+host.Failure+" / "+guest.Failure);
                Thread.Sleep(1);
            }
            guest.Send("rapid-join-"+i);string received=null;
            while(!host.Receive(out received)) {
                if(timer.Elapsed.TotalSeconds>6)throw new Exception("Rapid TCP join lost application frame");
                Thread.Sleep(1);
            }
            if(received!="rapid-join-"+i)throw new Exception("Rapid TCP join changed frame");
            checks++;
        }
        Console.WriteLine("Transport connection regression: 24 rapid production TCP joins deliver their application frame.");
        return checks;
    }
}
