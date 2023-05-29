using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GyuNet
{
    class TcpGyuNet : GyuNet
    {
        #region Public Method

        public override void Start()
        {
            base.Start();

            GyuNetPool.EventArgs.Spawned += OnSpawnEventArgs;
            GyuNetPool.EventArgs.Despawned += OnDespawnEventArgs;

            InitListenSocket();
            
            Task.Run(Update, serverTerminateCancellationTokenSource.Token);
        }

        #endregion

        #region Private Method

        private void InitListenSocket()
        {
            try
            {
                Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                Socket.Bind(new IPEndPoint(IPAddress.Any, Define.TCP_PORT));
                Socket.Listen(10);
            }
            catch (Exception e)
            {
                Debug.LogError("TCP Listener Start 예외 발생");
                Debug.LogError(e);
                Stop();
            }
            
            StartAccept();
        }

        protected override void OnAccept(SocketAsyncEventArgs e)
        {
            if (IsRunning == false)
                return;
            
            if (e.SocketError != SocketError.Success)
            {
                Debug.LogError("OnAccept Error");
                return;
            }
            
            var session = UserSession.Pool.Pop();
            session.ID = unchecked(sessionID++);
            session.Socket = e.AcceptSocket;

            var eventArgs = GyuNetPool.EventArgs.Pop();
            eventArgs.UserToken = session;
            base.OnAccept(eventArgs);
            
            StartAccept();
            lock (ConnectedSessions)
                ConnectedSessions.Add(session);
            StartReceive(eventArgs);
        }

        protected override void OnSend(SocketAsyncEventArgs e)
        {
            if (IsRunning == false)
                return;
            
            if (e.SocketError != SocketError.Success)
            {
                Debug.LogError("OnSend Error");
                return;
            }
            if (e.UserToken is Packet packet)
                Packet.Pool.Push(packet);
            // Send에 사용되는 버퍼는 메모리 풀에서 가져온게 아니라 Packet 버퍼를 가져온 것이다.
            // 미리 비워두자.
            e.SetBuffer(null, 0, 0);
            GyuNetPool.EventArgs.Push(e);
        }

        protected override void OnReceive(SocketAsyncEventArgs e)
        {
            if (IsRunning == false)
                return;
            
            if (e.SocketError == SocketError.Success && e.BytesTransferred > 0)
            {
                if (e.UserToken is UserSession session)
                {
                    session.ReceivePacket(e.Buffer, e.BytesTransferred);
                    StartReceive(e);
                }
            }
            else
            {
                if (e.BytesTransferred == 0)
                {
                    OnDisconnect(e);
                }
                else
                {
                    Debug.LogError($"OnReceive Error: {e.SocketError}");
                }
            }
        }
        
        protected override void OnDisconnect(SocketAsyncEventArgs e)
        {
            if (IsRunning == false)
                return;
            base.OnDisconnect(e);
            var session = e.UserToken as UserSession;
            UserSession.Pool.Push(session);
            GyuNetPool.EventArgs.Push(e);
        }

        #endregion
    }
}
