using System;
using System.Net;
using System.Net.Sockets;

namespace GyuNet
{
    public class TcpGyuNet : GyuNet
    {
        #region Public Method

        public override void Start()
        {
            base.Start();

            InitListenSocket();
        }

        #endregion

        #region Private Method

        private void InitListenSocket()
        {
            try
            {
                ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                ServerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                ServerSocket.Bind(new IPEndPoint(IPAddress.Any, Define.TCP_PORT));
                ServerSocket.Listen(10);
            }
            catch (Exception e)
            {
                Debug.LogError("TCP Listener Start 예외 발생");
                Debug.LogError(e);
                Stop();
            }
            
            StartAccept();
        }

        public override void StartSend(Session session, Packet packet)
        {
            if (session is TCPSession tcpSession)
            {
                if (tcpSession.Socket.Connected == false)
                    return;
                var eventArgs = GyuNetPool.EventArgs.Pop();
                eventArgs.UserToken = packet;
                eventArgs.SetBuffer(packet.Buffer, 0, packet.WriteOffset);
                if (!tcpSession.Socket.SendAsync(eventArgs))
                {
                    EventArgsOnCompleted(null, eventArgs);
                }
            }
        }

        protected override void StartReceive(SocketAsyncEventArgs e)
        {
            if (IsRunning == false)
                return;

            if (!(e.UserToken is TCPSession session))
                return;
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

        protected override void OnAccept(SocketAsyncEventArgs e)
        {
            if (IsRunning == false)
                return;
            StartAccept();
            if (e.SocketError != SocketError.Success)
            {
                Debug.LogError("OnAccept Error");
                return;
            }
            
            var session = TCPSession.Pool.Pop();
            while (ConnectedSessions.ContainsKey(sessionID))
                unchecked
                {
                    sessionID++;
                }
            session.Connected = true;
            session.ID = sessionID;
            session.Socket = e.AcceptSocket;

            var eventArgs = GyuNetPool.EventArgs.Pop();
            eventArgs.UserToken = session;
            ConnectedSessions.TryAdd(session.ID, session);
            base.OnAccept(eventArgs);
            
            StartReceive(eventArgs);
            GyuNetPool.EventArgs.Push(e);
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
                if (e.UserToken is TCPSession session)
                {
                    if (session.Connected == false)
                    {
                        return;
                    }
                    session.ReceiveData(e.Buffer, e.BytesTransferred);
                    StartReceive(e);
                    while (session.ReceivedPacketQueue.TryDequeue(out var rPacket))
                    {
                        OnReceivedPacket?.Invoke(this, session, rPacket);
                        Packet.Pool.Push(rPacket);
                    }
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
            var session = e.UserToken as TCPSession;
            TCPSession.Pool.Push(session);
            GyuNetPool.EventArgs.Push(e);
        }

        #endregion
    }
}
