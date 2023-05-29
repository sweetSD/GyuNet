using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace GyuNet
{
    abstract class GyuNet
    {
        protected Socket Socket;
        protected CancellationTokenSource serverTerminateCancellationTokenSource = null;
        
        protected readonly List<UserSession> ConnectedSessions = new List<UserSession>();
        protected readonly ConcurrentQueue<Packet> ReceivedPacketQueue = new ConcurrentQueue<Packet>();
        
        protected DateTime prevUpdateTime = DateTime.MinValue;
        public float DeltaTime => (float)(DateTime.Now - prevUpdateTime).TotalSeconds;

        public event Action<UserSession> onAccepted;
        public event Action<UserSession, Packet> onReceivedPacket;
        public event Action<UserSession> onDisconnected;
        
        protected uint sessionID = 1;
        
        public bool IsRunning { get; private set; } = false;

        public virtual void Start()
        {
            serverTerminateCancellationTokenSource = new CancellationTokenSource();
            IsRunning = true;
        }
        
        public virtual void Stop()
        {
            if (IsRunning == false)
                return;
            IsRunning = false;
            
            serverTerminateCancellationTokenSource?.Cancel();
            serverTerminateCancellationTokenSource?.Dispose();
            Socket?.Close();
        }
        
        protected void Update()
        {
            while (IsRunning)
            {
                lock (ConnectedSessions)
                {
                    foreach(var session in ConnectedSessions)
                    {
                        while (session.ReceivedPacketQueue.TryDequeue(out var rPacket))
                        {
                            ReceivedPacketQueue.Enqueue(rPacket);
                        }

                        while (session.SendPacketQueue.TryDequeue(out var sPacket))
                        {
                            StartSend(session, sPacket);
                        }
                    }
                }

                while (ReceivedPacketQueue.TryDequeue(out var packet))
                {
                    onReceivedPacket?.Invoke(null, packet);
                    Packet.Pool.Push(packet);
                }

                // Delta Time 구하기 위한 Update 시점 시간
                prevUpdateTime = DateTime.Now;
            }
        }
        
        protected void StartAccept()
        {
            if (IsRunning == false)
                return;
            
            SocketAsyncEventArgs acceptEventArgs = GyuNetPool.EventArgs.Pop();
            var pending = Socket.AcceptAsync(acceptEventArgs);
            if (!pending)
            {
                EventArgsOnCompleted(null, acceptEventArgs);
            }
        }
        
        protected void StartSend(UserSession session, Packet packet)
        {
            if (session.Socket.Connected == false)
                return;
            var eventArgs = GyuNetPool.EventArgs.Pop();
            eventArgs.UserToken = packet;
            eventArgs.SetBuffer(packet.Buffer, 0, packet.WriteOffset);
            session.Socket.SendAsync(eventArgs);
        }
        
        protected void StartReceive(SocketAsyncEventArgs e)
        {
            if (IsRunning == false)
                return;
            
            var session = e.UserToken as UserSession;
            if (session == null)
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
        
        protected virtual void OnSpawnEventArgs(SocketAsyncEventArgs args)
        {
            args.Completed += EventArgsOnCompleted;
        }

        protected virtual void OnDespawnEventArgs(SocketAsyncEventArgs args)
        {
            args.Completed -= EventArgsOnCompleted;
            if (args.Buffer != null)
            {
                GyuNetPool.Memories.Push(args.Buffer);
            }
            args.SetBuffer(null, 0, 0);
        }
        
        protected virtual void EventArgsOnCompleted(object sender, SocketAsyncEventArgs e)
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

        protected virtual void OnAccept(SocketAsyncEventArgs e)
        {
            onAccepted?.Invoke(e.UserToken as UserSession);
        }

        protected abstract void OnSend(SocketAsyncEventArgs e);

        protected abstract void OnReceive(SocketAsyncEventArgs e);

        protected virtual void OnDisconnect(SocketAsyncEventArgs e)
        {
            onDisconnected?.Invoke(e.UserToken as UserSession);
        }
    }
}
