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
        
        private Socket serverSocket;
        private CancellationTokenSource serverTerminateCancellationTokenSource = null;
        
        public void Start()
        {
            IsRunning = true;
            serverTerminateCancellationTokenSource = new CancellationTokenSource();

            GyuNetPool.EventArgs.Spawned += OnSpawnEventArgs;
            GyuNetPool.EventArgs.Despawned += OnDespawnEventArgs;

            InitListenSocket();

            Task.Run(Update, serverTerminateCancellationTokenSource.Token);
        }

        public void Update()
        {
            while (IsRunning)
            {
                
            }
        }

        public void Stop()
        {
            if (IsRunning == false)
                return;
            IsRunning = false;
            serverTerminateCancellationTokenSource?.Cancel();
            serverTerminateCancellationTokenSource?.Dispose();
            serverSocket?.Close();
        }

        private void InitListenSocket()
        {
            try
            {
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                serverSocket.Bind(new IPEndPoint(IPAddress.Any, Define.TCP_PORT));
                serverSocket.Listen(10);
            }
            catch (Exception e)
            {
                Debug.LogError("TCP Listener Start 예외 발생");
                Debug.LogError(e);
                Stop();
            }
            
            StartAccept();
        }
        
        private void OnSpawnEventArgs(SocketAsyncEventArgs args)
        {
            args.Completed += EventArgsOnCompleted;
        }

        private void OnDespawnEventArgs(SocketAsyncEventArgs args)
        {
            args.Completed -= EventArgsOnCompleted;
            if (args.Buffer != null)
            {
                GyuNetPool.Memories.Push(args.Buffer);
                // 메모리 풀 반납
            }
            args.SetBuffer(null, 0, 0);
        }
        
        private void EventArgsOnCompleted(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    OnAccept(e);
                    break;
                case SocketAsyncOperation.Send:
                    OnSend(e);
                    break;
                case SocketAsyncOperation.Receive:
                    OnReceive(e);
                    break;
                case SocketAsyncOperation.Disconnect:
                    OnDisconnect(e);
                    break;
                default:
                    throw new Exception("잘못된 작업이 완료되었습니다.");
            }
        }

        private void StartAccept()
        {
            if (IsRunning == false)
                return;
            SocketAsyncEventArgs acceptEventArgs = GyuNetPool.EventArgs.Pop();
            var pending = serverSocket.AcceptAsync(acceptEventArgs);
            if (!pending)
            {
                EventArgsOnCompleted(null, acceptEventArgs);
            }
        }

        private void OnAccept(SocketAsyncEventArgs e)
        {
            if (IsRunning == false)
                return;
            if (e.SocketError == SocketError.Success)
            {
                var eventArgs = GyuNetPool.EventArgs.Pop();
                var session = new Session();
                session.Socket = e.AcceptSocket;
                eventArgs.UserToken = session;
                StartReceive(eventArgs);
                StartAccept();
                
                Debug.Log($"새로운 클라이언트 접속: {session.Socket.RemoteEndPoint}");
            }
            else
            {
                Debug.LogError("OnAccept Error");
            }
        }

        private void OnSend(SocketAsyncEventArgs e)
        {
            if (IsRunning == false)
                return;
        }
        
        private void StartReceive(SocketAsyncEventArgs e)
        {
            if (IsRunning == false)
                return;
            var session = e.UserToken as Session;
            if (e.Buffer == null)
            {
                var buffer = GyuNetPool.Memories.Pop();
                if (buffer != null)
                    e.SetBuffer(buffer, 0, buffer.Length);
                else
                {
                    Debug.LogError("메모리 풀에서 메모리를 가져오지 못했습니다.");
                    return;
                }
            }
            var pending = session.Socket.ReceiveAsync(e);
            if (!pending)
            {
                EventArgsOnCompleted(null, e);
            }
        }
        
        private void OnReceive(SocketAsyncEventArgs e)
        {
            if (IsRunning == false)
                return;
            if (e.SocketError == SocketError.Success && e.BytesTransferred > 0)
            {
                if (e.UserToken is Session session)
                {
                    session.OnPacketProcess(e.Buffer, e.BytesTransferred);
                    StartReceive(e);
                }
            }
            else
            {
                Debug.LogError($"OnReceive Error: {e.SocketError}");
            }
        }
        
        private void OnDisconnect(SocketAsyncEventArgs e)
        {
            if (IsRunning == false)
                return;
            if (e.SocketError == SocketError.Disconnecting && e.BytesTransferred > 0)
            {
                GyuNetPool.EventArgs.Push(e);
            }
            else
            {
                Debug.LogError("OnReceive Error");
            }
        }
    }
}
