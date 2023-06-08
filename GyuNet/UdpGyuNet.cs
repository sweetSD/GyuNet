using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace GyuNet
{
    public class UdpGyuNet : GyuNet
    {
        protected readonly ConcurrentDictionary<int, Session> SessionEndPointDictionary = new ConcurrentDictionary<int, Session>();
        
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
                ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                ServerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                ServerSocket.Bind(new IPEndPoint(IPAddress.Any, Define.UDP_PORT));
                
                var eventArgs = GyuNetPool.EventArgs.Pop();
                StartReceive(eventArgs);
            }
            catch (Exception e)
            {
                Debug.LogError("UDP Socket 예외 발생");
                Debug.LogError(e);
                Stop();
            }
        }

        public override void StartSend(Session session, Packet packet)
        {
            if (session is UDPSession udpSession)
            {
                var eventArgs = GyuNetPool.EventArgs.Pop();
                eventArgs.RemoteEndPoint = udpSession.EndPoint;
                eventArgs.UserToken = packet;
                eventArgs.SetBuffer(packet.Buffer, 0, packet.WriteOffset);
                if (!ServerSocket.SendToAsync(eventArgs))
                {
                    EventArgsOnCompleted(null, eventArgs);
                }
            }
        }

        protected override void StartReceive(SocketAsyncEventArgs e)
        {
            if (IsRunning == false)
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
            e.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            if (!ServerSocket.ReceiveMessageFromAsync(e))
            {
                EventArgsOnCompleted(null, e);
            }
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
            
            var session = UDPSession.Pool.Pop();
            while (ConnectedSessions.ContainsKey(sessionID))
                unchecked
                {
                    sessionID++;
                }
            session.ID = sessionID;
            session.EndPoint = e.RemoteEndPoint;

            e.UserToken = session;
            SessionEndPointDictionary.TryAdd(session.EndPoint.ToString().GetHashCode(), session);
            ConnectedSessions.TryAdd(session.ID, session);
            base.OnAccept(e);
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
                var hashCode = e.RemoteEndPoint.ToString().GetHashCode();
                
                if (!SessionEndPointDictionary.ContainsKey(hashCode))
                {
                    OnAccept(e);
                }
                
                if (SessionEndPointDictionary.TryGetValue(hashCode, out var session))
                {
                    session.ReceiveData(e.Buffer, e.BytesTransferred);
                    while (session.ReceivedPacketQueue.TryDequeue(out var rPacket))
                    {
                        OnReceivedPacket?.Invoke(this, session, rPacket);
                    }
                }
            }
        }

        protected override void OnDisconnect(SocketAsyncEventArgs e)
        {
            if (IsRunning == false)
                return;
            base.OnDisconnect(e);
            var session = e.UserToken as UDPSession;
            UDPSession.Pool.Push(session);
            GyuNetPool.EventArgs.Push(e);
        }

        #endregion
    }
}
