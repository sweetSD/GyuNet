using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GyuNet
{
    public abstract class GyuNet
    {
        protected Socket ServerSocket;
        protected CancellationTokenSource ServerTerminateCancellationTokenSource = null;
        
        protected readonly ConcurrentDictionary<int, Session> ConnectedSessions = new ConcurrentDictionary<int, Session>();

        public Action<GyuNet, Session> OnAccepted;
        public Action<GyuNet, Session, Packet> OnReceivedPacket;
        public Action<GyuNet, Session> OnDisconnected;
        
        protected int sessionID = 1;
        
        public bool IsRunning { get; private set; } = false;

        public virtual void Start()
        {
            ServerTerminateCancellationTokenSource = new CancellationTokenSource();
            IsRunning = true;
            
            GyuNetPool.EventArgs.Spawned += OnSpawnEventArgs;
            GyuNetPool.EventArgs.Despawned += OnDespawnEventArgs;
            
            Task.Run(Update, ServerTerminateCancellationTokenSource.Token);
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
            }
        }
        
        protected void StartAccept()
        {
            if (IsRunning == false)
                return;
            
            SocketAsyncEventArgs acceptEventArgs = GyuNetPool.EventArgs.Pop();
            if (!ServerSocket.AcceptAsync(acceptEventArgs))
            {
                EventArgsOnCompleted(null, acceptEventArgs);
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
            args.RemoteEndPoint = null;
            args.AcceptSocket = null;
            args.UserToken = null;
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
                case SocketAsyncOperation.SendTo:
                    OnSend(e);
                    break;
                case SocketAsyncOperation.Receive:
                case SocketAsyncOperation.ReceiveFrom:
                case SocketAsyncOperation.ReceiveMessageFrom:
                    OnReceive(e);
                    break;
                case SocketAsyncOperation.Disconnect:
                    OnDisconnect(e);
                    break;
                default:
                    throw new Exception("잘못된 작업이 완료되었습니다.");
            }
        }

        public void StartSend(Packet packet, params int[] excludes)
        {
            foreach (var session in ConnectedSessions)
            {
                if (excludes != null && Array.IndexOf(excludes, session.Key) != -1)
                    continue;
                StartSend(session.Value, packet);
            }
        }
        
        public abstract void StartSend(Session session, Packet packet);

        protected abstract void StartReceive(SocketAsyncEventArgs e);

        protected virtual void OnAccept(SocketAsyncEventArgs e)
        {
            OnAccepted?.Invoke(this, e.UserToken as Session);
        }

        protected abstract void OnSend(SocketAsyncEventArgs e);

        protected abstract void OnReceive(SocketAsyncEventArgs e);

        protected virtual void OnDisconnect(SocketAsyncEventArgs e)
        {
            OnDisconnected?.Invoke(this, e.UserToken as Session);
        }
    }
}
