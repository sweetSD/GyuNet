using System.Collections.Concurrent;
using System.Collections.Generic;

namespace GyuNet
{
    namespace Unity
    {
        public enum PacketHeader : short
        {
            Ping = 0,
            Pong,
            RequestRoomJoin,
            RoomJoin,
            RequestRoomLeave,
            RoomLeave,
            RequestObjectSpawn,
            ObjectSpawn,
            RequestObjectSync,
            ObjectSync,
            RequestObjectDespawn,
            ObjectDespawn,
            Chat,
            Rpc
        }
        
        class Room
        {
            public List<Session> Sessions = new List<Session>();
        }

        class Unity
        {
            private TcpGyuNet tcpGyuNet = new TcpGyuNet();

            private SortedSet<int> networkIDs = new SortedSet<int>();
            private int networkID = 0;

            private ConcurrentBag<Room> Rooms { get; } = new ConcurrentBag<Room>();
            private ConcurrentDictionary<int, Room> SessionRoomPair { get; } = new ConcurrentDictionary<int, Room>();

            public bool IsRunning => tcpGyuNet.IsRunning;

            public void Start()
            {
                tcpGyuNet.OnAccepted += OnAccepted;
                tcpGyuNet.OnReceivedPacket += OnReceivePacket;
                tcpGyuNet.OnDisconnected += OnDisconnect;

                tcpGyuNet.Start();
            }

            public void Stop()
            {
                tcpGyuNet.Stop();
            }

            void OnAccepted(GyuNet net, Session session)
            {
                if (session is TCPSession tcpSession)
                    Debug.Log($"TCP 새로운 클라이언트 접속: {tcpSession.Socket.RemoteEndPoint}");
                else if (session is UDPSession udpSession)
                    Debug.Log($"UDP 새로운 클라이언트 접속: {udpSession.EndPoint}");
            }

            void OnReceivePacket(GyuNet net, Session session, Packet packet)
            {
                switch ((PacketHeader)packet.Header)
                {
                    case PacketHeader.Ping:
                    {
                        // 플레이어 접속. 플레이어 ID 부여 후 로비로 전환
                        var pongPacket = new Packet();
                        pongPacket.Serialize(session.ID);
                        pongPacket.SetHeader((short)PacketHeader.Pong);
                        net.StartSend(session, pongPacket);
                        break;
                    }
                    case PacketHeader.RequestRoomJoin:
                        // 플레이어 방 입장.
                        OnRequestRoomJoin(net, session, packet);
                        break;
                    case PacketHeader.RequestRoomLeave:
                        // 플레이어 방 퇴장.
                        OnRequestRoomLeave(net, session, packet);
                        break;
                    case PacketHeader.RequestObjectSpawn:
                        // 네트워크 오브젝트 생성.
                        OnRequestObjectSpawn(net, session, packet);
                        break;
                    case PacketHeader.RequestObjectSync:
                        // 네트워크 오브젝트 동기화
                        OnRequestObjectSync(net, session, packet);
                        break;
                    case PacketHeader.RequestObjectDespawn:
                        // 네트워크 오브젝트 파괴
                        OnRequestObjectDespawn(net, session, packet);
                        break;
                    case PacketHeader.Chat:
                        // 채팅.
                        OnChat(net, session, packet);
                        break;
                    case PacketHeader.Rpc:
                        // Rpc.
                        break;
                }
            }

            void OnDisconnect(GyuNet net, Session session)
            {
                var tcpSession = session as TCPSession;
                Debug.Log($"클라이언트 접속 종료: {tcpSession?.Socket.RemoteEndPoint}");
            }

            void OnRequestRoomJoin(GyuNet net, Session session, Packet packet)
            {
                if (SessionRoomPair.TryGetValue(session.ID, out _)) return;
                Room joinRoom = null;
                foreach (var room in Rooms)
                {
                    lock (room.Sessions)
                    {
                        if (room.Sessions.Count < 4)
                        {
                            joinRoom = room;
                            break;
                        }
                    }
                }
                if (joinRoom == null)
                {
                    joinRoom = new Room();
                    Rooms.Add(joinRoom);
                }
                Packet joinPacket = new Packet();
                lock (joinRoom.Sessions)
                {
                    joinRoom.Sessions.Add(session);
                    SessionRoomPair.TryAdd(session.ID, joinRoom);
                            
                    joinPacket.Serialize(joinRoom.Sessions.Count);
                    foreach (var roomSession in joinRoom.Sessions)
                    {
                        joinPacket.Serialize(roomSession.ID);
                    }
                }
                joinPacket.SetHeader((short)PacketHeader.RoomJoin);
                net.StartSend(session, joinPacket);
            }

            void OnRequestRoomLeave(GyuNet net, Session session, Packet packet)
            {
                var sessionId = session.ID;
                if (SessionRoomPair.TryGetValue(sessionId, out var room))
                {
                    lock (room.Sessions)
                    {
                        room.Sessions.Remove(session);
                        SessionRoomPair.TryRemove(sessionId, out _);
                    }
                    Packet leavePacket = new Packet();
                    leavePacket.SetHeader((short)PacketHeader.RoomLeave);
                    net.StartSend(session, leavePacket);
                }
            }

            void OnRequestObjectSpawn(GyuNet net, Session session, Packet packet)
            {
                lock (networkIDs)
                    while (networkIDs.Contains(networkID))
                        unchecked
                        {
                            networkID++;
                        }
                var spawnPacket = new Packet();
                spawnPacket.Serialize(networkID);
                spawnPacket.Serialize(packet.DeserializeString());
                spawnPacket.Serialize(packet.DeserializeVector3());
                spawnPacket.Serialize(packet.DeserializeVector3());
                spawnPacket.Serialize(packet.DeserializeInt());
                spawnPacket.SetHeader((short)PacketHeader.ObjectSpawn);
                net.StartSend(spawnPacket);
            }

            void OnRequestObjectSync(GyuNet net, Session session, Packet packet)
            {
                var syncPacket = new Packet();
                syncPacket.CopyBuffer(packet.Buffer, Define.HEADER_SIZE, packet.WriteOffset - Define.HEADER_SIZE);
                syncPacket.SetHeader((short)PacketHeader.ObjectSync);
                net.StartSend(syncPacket);
            }

            void OnRequestObjectDespawn(GyuNet net, Session session, Packet packet)
            {
                var despawnPacket = new Packet();
                despawnPacket.Serialize(packet.DeserializeInt());
                despawnPacket.SetHeader((short)PacketHeader.ObjectDespawn);
                net.StartSend(despawnPacket);
            }

            void OnChat(GyuNet net, Session session, Packet packet)
            {
                var sessionId = session.ID;
                if (SessionRoomPair.TryGetValue(sessionId, out var room))
                {
                    lock (room.Sessions)
                    {
                        foreach (var roomSession in room.Sessions)
                        {
                            var chatPacket = new Packet();
                            chatPacket.Serialize(session.ID);
                            chatPacket.Serialize(packet.DeserializeString());
                            chatPacket.SetHeader((short)PacketHeader.Chat);
                            net.StartSend(roomSession, chatPacket);
                        }
                    }
                }
            }
        }
    }
}