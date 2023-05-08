using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GyuNet
{
    interface INet
    {
        void Start();
        void Update();
        void Stop();
    }
    
    class TcpGyuNet : INet
    {
        public bool IsRunning { get; private set; } = false;
        
        private TcpListener tcpListener;
        private GyuNetIOCP iocp;
        
        private readonly object clientListLock = new object();
        private readonly List<TcpClient> clientList = new List<TcpClient>();
        
        private CancellationTokenSource serverTerminateCancellationTokenSource = null;
        
        public void Start()
        {
            IsRunning = true;
            serverTerminateCancellationTokenSource = new CancellationTokenSource();
            iocp = new GyuNetIOCP(serverTerminateCancellationTokenSource.Token);

            Task.Run(StartListener, serverTerminateCancellationTokenSource.Token);
            Task.Run(Update, serverTerminateCancellationTokenSource.Token);
        }

        public void Update()
        {
            while (IsRunning)
            {
                while (iocp.ReceivedPacketQueue.IsEmpty == false)
                {
                    if (iocp.ReceivedPacketQueue.TryDequeue(out var packet))
                    {
                        Debug.Log(packet.Header);

                        switch (packet.Header)
                        {
                            case PacketHeader.PING:
                                Debug.Log("Ping!");
                                break;
                            case PacketHeader.PONG:
                                Debug.Log("Pong!");
                                break;
                        }
                    }
                }
            }
        }

        public void Stop()
        {
            if (IsRunning == false)
                return;
            IsRunning = false;
            serverTerminateCancellationTokenSource?.Cancel();
            serverTerminateCancellationTokenSource?.Dispose();
            tcpListener?.Stop();
        }

        private async void StartListener()
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, Define.TCP_PORT);
                tcpListener?.Start();
            }
            catch (Exception e)
            {
                Debug.LogError("TCP Listener Start 예외 발생");
                Debug.LogError(e);
                Stop();
            }
            
            while (IsRunning)
            {
                try
                {
                    Debug.Log("Start Accept");
                    var tcpClient = await (tcpListener?.AcceptTcpClientAsync() ?? Task.FromResult<TcpClient>(null));
                    Debug.Log("Accept Success");

                    if (tcpClient != null)
                    {
                        lock (clientListLock)
                        {
                            iocp.AddClient(tcpClient.Client);
                        }
                    }
                    Debug.Log($"새로운 클라이언트 연결됨. {tcpClient?.Client.RemoteEndPoint}");
                }
                catch (Exception e)
                {
                    Debug.LogError("TCP Listener AcceptTcpClientAsync 예외 발생");
                    Debug.LogError(e);
                    Stop();
                }
            }
        }
    }
}
