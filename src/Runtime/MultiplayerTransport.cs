using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace GTIP
{
    // One peer, trusted LAN/VPN only. All Unity access is confined to the main-thread adapter.
    public sealed class MpTransport : IDisposable
    {
        // Exact, protocol-versioned control frame. No session/campaign mutation is
        // encoded here; anything except this exact frame reaches normal validation.
        static readonly string heartbeat=MpCodec.Encode(new MpMessage{kind="transport-heartbeat"});
        const double HeartbeatSeconds=1;
        readonly MpFrameQueue incoming = new MpFrameQueue();
        readonly MpFrameQueue outgoing = new MpFrameQueue();
        readonly object gate = new object();
        TcpListener listener;
        TcpClient client;
        volatile bool closed, connected;
        string failure;
        int heartbeatsSent,heartbeatsReceived;
        public int HeartbeatsSent => Volatile.Read(ref heartbeatsSent);
        public int HeartbeatsReceived => Volatile.Read(ref heartbeatsReceived);
        public int PendingIncoming => incoming.Count;
        public int PendingOutgoing => outgoing.Count;
        public int PeakIncoming => incoming.Peak;
        public int CoalescedIncoming => incoming.Coalesced;
        public int CoalescedOutgoing => outgoing.Coalesced;
        public bool Connected => connected && !closed;
        public string Failure => failure;
        public bool Closed => closed;
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        public static MpTransport Host(IPAddress address, int port)
        {
            var transport = new MpTransport();
            transport.listener = new TcpListener(address, port);
            transport.listener.Start(1);
            transport.Start(() => transport.Open(transport.listener.AcceptTcpClient()));
            return transport;
        }
        public static MpTransport Join(IPAddress address, int port)
        {
            var transport = new MpTransport();
            transport.client = new TcpClient();
            transport.Start(() => {
                var result = transport.client.BeginConnect(address, port, null, null);
                using (result.AsyncWaitHandle) { if (!result.AsyncWaitHandle.WaitOne(5000)) throw new TimeoutException("Connection timed out."); }
                transport.client.EndConnect(result); transport.Open(transport.client);
            });
            return transport;
        }
        void Start(Action action)
        {
            new Thread(() => { try { action(); } catch (Exception e) { if (!closed) { failure = DescribeFailure(e); Dispose(); } } }) { IsBackground = true, Name = "GTIP multiplayer" }.Start();
        }
        static string DescribeFailure(Exception error)
        {
            var socket=error as SocketException??error.InnerException as SocketException;
            return error.GetType().Name+(socket==null?"":" (socket="+socket.SocketErrorCode+")")+": "+error.Message.Replace("\0","").Trim();
        }
        void Open(TcpClient peer)
        {
            lock (gate) { if (closed) { peer.Close(); return; } client = peer; }
            listener?.Stop();
            peer.NoDelay = true; peer.ReceiveTimeout = 15000; peer.SendTimeout = 5000;
            var stream = peer.GetStream(); connected = true;
            Start(() => {
                long lastWrite=Stopwatch.GetTimestamp();
                while (!closed) {
                    if (outgoing.TryTake(out var text)) {
                        MpRules.WriteFrame(stream,text);lastWrite=Stopwatch.GetTimestamp();
                    } else if((Stopwatch.GetTimestamp()-lastWrite)/(double)Stopwatch.Frequency>=HeartbeatSeconds) {
                        // Unity can stop calling Update while it loads a campaign.
                        // Socket liveness must be independent of that main thread.
                        MpRules.WriteFrame(stream,heartbeat);Interlocked.Increment(ref heartbeatsSent);lastWrite=Stopwatch.GetTimestamp();
                    }
                    else Thread.Sleep(10);
                }
            });
            while (!closed) {
                var text = MpRules.ReadFrame(stream);
                if(string.Equals(text,heartbeat,StringComparison.Ordinal)) {Interlocked.Increment(ref heartbeatsReceived);continue;}
                // A native state application may hold Unity's thread for seconds.
                // Bound memory and let TCP exert backpressure until it can drain.
                if(!incoming.AddWaiting(text))break;
            }
        }
        public bool Receive(out string text)
        { return incoming.TryTake(out text); }
        public void Send(string text)
        {
            if (closed) return;
            if (text == null || text.Length > MpRules.MaxBytes || !outgoing.TryAdd(text))
            { failure = "Peer is not consuming messages."; Dispose(); return; }
        }
        public void Dispose()
        {
            lock (gate) { closed = true; connected = false; incoming.Close();outgoing.Close();try { listener?.Stop(); } catch { } try { client?.Close(); } catch { } }
        }
    }
    // A clock supersedes only a consecutive clock for the same session. Reliable
    // frames remain FIFO barriers; commands/chunks/results are never coalesced.
    public sealed class MpFrameQueue
    {
        sealed class Frame
        {
            public string Text,ClockSession;public int Sequence;
            public Frame(string text){Text=text;MpCodec.ReplaceableClock(text,out ClockSession,out Sequence);}
        }
        readonly object gate=new object();
        readonly Queue<Frame> frames=new Queue<Frame>();
        readonly int capacity;
        Frame tail;
        bool closed;
        int peak,coalesced;
        public MpFrameQueue(int capacity=64){if(capacity<1 || capacity>1024)throw new ArgumentOutOfRangeException(nameof(capacity));this.capacity=capacity;}
        public int Count {get{lock(gate)return frames.Count;}}
        public int Peak {get{lock(gate)return peak;}}
        public int Coalesced {get{lock(gate)return coalesced;}}
        bool Replace(Frame frame)
        {
            if(frame.ClockSession==null || tail==null || frame.ClockSession!=tail.ClockSession)return false;
            if(frame.Sequence>tail.Sequence){tail.Text=frame.Text;tail.Sequence=frame.Sequence;}
            coalesced++;return true;
        }
        void Append(Frame frame){frames.Enqueue(frame);tail=frame;peak=Math.Max(peak,frames.Count);}
        public bool TryAdd(string text)
        {
            var frame=new Frame(text);
            lock(gate){if(closed)return false;if(Replace(frame))return true;if(frames.Count>=capacity)return false;Append(frame);return true;}
        }
        public bool AddWaiting(string text)
        {
            var frame=new Frame(text);
            lock(gate) {
                while(!closed) {
                    if(Replace(frame))return true;
                    if(frames.Count<capacity){Append(frame);return true;}
                    Monitor.Wait(gate);
                }
                return false;
            }
        }
        public bool TryTake(out string text)
        {
            lock(gate) {
                if(frames.Count==0){text=null;return false;}
                text=frames.Dequeue().Text;if(frames.Count==0)tail=null;Monitor.PulseAll(gate);return true;
            }
        }
        public void Close(){lock(gate){closed=true;Monitor.PulseAll(gate);}}
    }
}
