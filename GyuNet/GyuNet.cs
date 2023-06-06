using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace GyuNet
{
    abstract class GyuNet
    {
        protected Socket ServerSocket;
        protected CancellationTokenSource ServerTerminateCancellationTokenSource = null;
        
        protected readonly ConcurrentDictionary<uint, Session> ConnectedSessions = new ConcurrentDictionary<uint, Session>();
        protected readonly ConcurrentQueue<Packet> ReceivedPacketQueue = new ConcurrentQueue<Packet>();

        public event Action<Session> onAccepted;
        public event Action<Session, Packet> onReceivedPacket;
        public event Action<Session> onDisconnected;
        
        protected uint sessionID = 1;
        
        public bool IsRunning { get; private set; } = false;

        public virtual void Start()
        {
            ServerTerminateCancellationTokenSource = new CancellationTokenSource();
            IsRunning = true;
        }
        
        public virtual void Stop()
        {
            if (IsRunning == false)
                return;
            IsRunning = false;
            
            ServerTerminateCancellationTokenSource?.Cancel();
            ServerTerminateCancellationTokenSource?.Dispose();
            ServerSocket?.Close();
        }
        
        protected void Update()
        {
            while (IsRunning)
            {
                foreach (var session in ConnectedSessions)
                {
                    while (session.Value.SendPacketQueue.TryDequeue(out var sPacket))
                    {
                        StartSend(session.Value, sPacket);
                    }
                }

                while (ReceivedPacketQueue.TryDequeue(out var packet))
                {
                    onReceivedPacket?.Invoke(null, packet);
                    Packet.Pool.Push(packet);
                }
            }
        }
        
        protected void StartAccept()
        {
            if (IsRunning == false)
                return;
            
            SocketAsyncEventArgs acceptEventArgs = GyuNetPool.EventArgs.Pop();
            var pending = ServerSocket.AcceptAsync(acceptEventArgs);
            if (!pending)
            {
                EventArgsOnCompleted(null, acceptEventArgs);
            }
        }

        protected abstract void StartSend(Session session, Packet packet);

        protected abstract void StartReceive(SocketAsyncEventArgs e);
        
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
            onAccepted?.Invoke(e.UserToken as Session);
        }

        protected abstract void OnSend(SocketAsyncEventArgs e);

        protected abstract void OnReceive(SocketAsyncEventArgs e);

        protected virtual void OnDisconnect(SocketAsyncEventArgs e)
        {
            onDisconnected?.Invoke(e.UserToken as Session);
        }
    }
}
