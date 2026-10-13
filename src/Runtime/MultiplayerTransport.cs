using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace GTIP
{
    // One peer, trusted LAN/VPN only. All Unity access is confined to the main-thread adapter.
    public sealed class MpTransport : IDisposable
    {
        readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> outgoing = new ConcurrentQueue<string>();
        readonly object gate = new object();
        TcpListener listener;
        TcpClient client;
        volatile bool closed, connected;
        string failure;
        int incomingCount, outgoingCount;
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
            new Thread(() => { try { action(); } catch (Exception e) { if (!closed) { failure = e.GetType().Name + ": " + e.Message; Dispose(); } } }) { IsBackground = true, Name = "GTIP multiplayer" }.Start();
        }
        void Open(TcpClient peer)
        {
            lock (gate) { if (closed) { peer.Close(); return; } client = peer; }
            listener?.Stop();
            peer.NoDelay = true; peer.ReceiveTimeout = 15000; peer.SendTimeout = 5000;
            var stream = peer.GetStream(); connected = true;
            Start(() => {
                while (!closed) {
                    if (outgoing.TryDequeue(out var text)) { Interlocked.Decrement(ref outgoingCount); MpRules.WriteFrame(stream, text); }
                    else Thread.Sleep(10);
                }
            });
            while (!closed) {
                var text = MpRules.ReadFrame(stream);
                if (Interlocked.Increment(ref incomingCount) > 64) throw new InvalidOperationException("Peer exceeded receive queue.");
                incoming.Enqueue(text);
            }
        }
        public bool Receive(out string text)
        { if (!incoming.TryDequeue(out text)) return false; Interlocked.Decrement(ref incomingCount); return true; }
        public void Send(string text)
        {
            if (closed) return;
            if (text == null || text.Length > MpRules.MaxBytes || Interlocked.Increment(ref outgoingCount) > 64)
            { failure = "Peer is not consuming messages."; Dispose(); return; }
            outgoing.Enqueue(text);
        }
        public void Dispose()
        {
            lock (gate) { closed = true; connected = false; try { listener?.Stop(); } catch { } try { client?.Close(); } catch { } }
        }
    }
}
