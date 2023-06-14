using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

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
            Rpc,
            RequestSetPlayerObject,
            SetPlayerObject,
            SetHostClient,
        }
        
        class Room
        {
            public int HostClient { get; set; } = -1;
            public List<Session> Sessions { get; } = new List<Session>();
            public ConcurrentDictionary<int, Object> SpawnedObjects { get; } = new ConcurrentDictionary<int, Object>();
            public ConcurrentDictionary<int, int> PlayerObjects { get; } = new ConcurrentDictionary<int, int>();
        }

        class Object
        {
            public int NetworkID;
            public string Prefab;
            public (float x, float y, float z) Position;
            public (float x, float y, float z) Rotation;
            public int Authority;

            public Object(int id, string name, (float x, float y, float z) pos, (float x, float y, float z) rot,
                int authority)
            {
                NetworkID = id;
                Prefab = name;
                Position = pos;
                Rotation = rot;
                Authority = authority;
            }
            
            public Object(Packet packet)
            {
                NetworkID = packet.DeserializeInt();
                Prefab = packet.DeserializeString();
                Position = packet.DeserializeVector3();
                Rotation = packet.DeserializeVector3();
                Authority = packet.DeserializeInt();
            }
        }

        class Unity
        {
            private TcpGyuNet tcpGyuNet = new TcpGyuNet();

            private List<Room> Rooms { get; } = new List<Room>();
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
                var header = (PacketHeader)packet.Header;
                //Debug.Log($"{session.ID} >> New Packet Received: {header} | Read: {packet.ReadOffset} | Write: {packet.WriteOffset}");
                switch (header)
                {
                    case PacketHeader.Ping:
                    {
                        // 플레이어 접속. 플레이어 ID 부여 후 로비로 전환
                        var pongPacket = Packet.Pool.Pop();
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
                        // 네트워크 오브젝트 동기화.
                        OnRequestObjectSync(net, session, packet);
                        break;
                    case PacketHeader.RequestObjectDespawn:
                        // 네트워크 오브젝트 파괴.
                        OnRequestObjectDespawn(net, session, packet);
                        break;
                    case PacketHeader.Chat:
                        // 채팅.
                        OnChat(net, session, packet);
                        break;
                    case PacketHeader.Rpc:
                        // Rpc.
                        OnRpc(net, session, packet);
                        break;
                    case PacketHeader.RequestSetPlayerObject:
                        // 플레이어 오브젝트 설정.
                        OnRequestSetPlayerObject(net, session, packet);
                        break;
                }
                Packet.Pool.Push(packet);
            }

            void OnDisconnect(GyuNet net, Session session)
            {
                OnRequestRoomLeave(net, session, null);
                var tcpSession = session as TCPSession;
                Debug.Log($"클라이언트 접속 종료: {tcpSession?.Socket.RemoteEndPoint}");
            }

            void SendPacketToRoom(GyuNet net, Room room, Packet packet, params int[] exclusive)
            {
                lock (room)
                {
                    foreach (var roomSession in room.Sessions)
                    {
                        if (Array.IndexOf(exclusive, roomSession.ID) != -1) continue;
                        var copyPacket = Packet.Pool.Pop();
                        copyPacket.CopyBuffer(packet.Buffer, Define.HEADER_SIZE, packet.WriteOffset - Define.HEADER_SIZE);
                        copyPacket.SetHeader(packet.Header);
                        roomSession.SendPacketQueue.Enqueue(copyPacket);
                    }
                }
            }

            void OnRequestRoomJoin(GyuNet net, Session session, Packet packet)
            {
                if (SessionRoomPair.TryGetValue(session.ID, out _)) return;
                Room joinRoom = null;
                lock (Rooms)
                {
                    foreach (var room in Rooms)
                    {
                        lock (room)
                        {
                            if (room.Sessions.Count < 4)
                            {
                                joinRoom = room;
                                break;
                            }
                        }
                    }
                }
                if (joinRoom == null)
                {
                    joinRoom = new Room()
                    {
                        HostClient = session.ID
                    };
                    lock(Rooms)
                        Rooms.Add(joinRoom);
                }
                lock (joinRoom)
                {
                    joinRoom.Sessions.Add(session);
                    SessionRoomPair.TryAdd(session.ID, joinRoom);
                }

                // 기존 방 플레이어들에게 새 플레이어 입장 패킷 전송
                Packet joinAlertPacket = Packet.Pool.Pop();
                joinAlertPacket.Serialize(session.ID);
                joinAlertPacket.SetHeader((short)PacketHeader.RoomJoin);
                SendPacketToRoom(net, joinRoom, joinAlertPacket, session.ID);

                Packet joinPacket = Packet.Pool.Pop();
                lock (joinRoom)
                {
                    // 호스트 플레이어 ID 패킷 직렬화
                    joinPacket.Serialize(joinRoom.HostClient);
                    
                    // 기존 방 플레이어들 ID 패킷 직렬화
                    joinPacket.Serialize(joinRoom.Sessions.Count);
                    foreach (var roomSession in joinRoom.Sessions)
                    {
                        joinPacket.Serialize(roomSession.ID);
                    }

                    // 설정된 플레이어 오브젝트 패킷 직렬화
                    joinPacket.Serialize(joinRoom.PlayerObjects.Count);
                    foreach (var spawnedObject in joinRoom.PlayerObjects)
                    {
                        joinPacket.Serialize(spawnedObject.Key);
                        joinPacket.Serialize(spawnedObject.Value);
                    }
                    
                    // 기존 방에 생성된 오브젝트 패킷 직렬화
                    joinPacket.Serialize(joinRoom.SpawnedObjects.Count);
                    foreach (var spawnedObject in joinRoom.SpawnedObjects)
                    {
                        Debug.Log("<= Serialize Spawned Object. =>");
                        Debug.Log(spawnedObject.Value.NetworkID);
                        Debug.Log(spawnedObject.Value.Prefab);
                        Debug.Log(spawnedObject.Value.Position);
                        Debug.Log(spawnedObject.Value.Rotation);
                        Debug.Log(spawnedObject.Value.Authority);
                        joinPacket.Serialize(spawnedObject.Value.NetworkID);
                        joinPacket.Serialize(spawnedObject.Value.Prefab);
                        joinPacket.Serialize(spawnedObject.Value.Position);
                        joinPacket.Serialize(spawnedObject.Value.Rotation);
                        joinPacket.Serialize(spawnedObject.Value.Authority);
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
                    lock (room)
                    {
                        foreach (var roomSession in room.Sessions)
                        {
                            Packet leavePacket = Packet.Pool.Pop();
                            leavePacket.Serialize(sessionId);
                            leavePacket.SetHeader((short)PacketHeader.RoomLeave);
                            net.StartSend(roomSession, leavePacket);
                        }
                        room.Sessions.Remove(session);
                        SessionRoomPair.TryRemove(sessionId, out _);
                        if (room.Sessions.FindIndex(e => e.ID == room.HostClient) == -1)
                        {
                            if (room.Sessions.Count > 0)
                            {
                                room.HostClient = room.Sessions[0].ID;
                                var hostPacket = Packet.Pool.Pop();
                                hostPacket.Serialize(room.HostClient);
                                hostPacket.SetHeader((short)PacketHeader.SetHostClient);
                                SendPacketToRoom(net, room, hostPacket);
                            }
                            else
                            {
                                lock (Rooms)
                                    Rooms.Remove(room);
                            }
                        }
                    }
                }
            }

            void OnRequestObjectSpawn(GyuNet net, Session session, Packet packet)
            {
                Debug.Log(packet.WriteOffset);
                if (SessionRoomPair.TryGetValue(session.ID, out var room))
                {
                    var spawnPacket = Packet.Pool.Pop();
                    lock (room)
                    {
                        var networkId = packet.DeserializeInt();
                        var prefab = packet.DeserializeString();
                        var position = packet.DeserializeVector3();
                        var rotation = packet.DeserializeVector3();
                        var authority = packet.DeserializeInt();
                        Debug.Log(networkId);
                        Debug.Log(prefab);
                        Debug.Log(position);
                        Debug.Log(rotation);
                        Debug.Log(authority);
                        spawnPacket.Serialize(networkId);
                        spawnPacket.Serialize(prefab);
                        spawnPacket.Serialize(position);
                        spawnPacket.Serialize(rotation);
                        spawnPacket.Serialize(authority);
                        spawnPacket.SetHeader((short)PacketHeader.ObjectSpawn);
                        room.SpawnedObjects.TryAdd(networkId, new Object(networkId, prefab, position, rotation, authority));
                    }
                    SendPacketToRoom(net, room, spawnPacket, session.ID);
                    Packet.Pool.Push(spawnPacket);
                }
            }

            void OnRequestObjectSync(GyuNet net, Session session, Packet packet)
            {
                if (SessionRoomPair.TryGetValue(session.ID, out var room))
                {
                    if (packet.WriteOffset == Define.HEADER_SIZE)
                    {
                        Debug.LogError("비어있는 Sync 패킷은 보내지 않습니다.");
                        return;
                    }
                    packet.SetHeader((short)PacketHeader.ObjectSync);
                    SendPacketToRoom(net, room, packet, session.ID);
                }
            }

            void OnRequestObjectDespawn(GyuNet net, Session session, Packet packet)
            {
                if (SessionRoomPair.TryGetValue(session.ID, out var room))
                {
                    var despawnPacket = Packet.Pool.Pop();
                    lock (room)
                    {
                        var networkID = packet.DeserializeInt();
                        despawnPacket.Serialize(networkID);
                        despawnPacket.SetHeader((short)PacketHeader.ObjectDespawn);
                        room.SpawnedObjects.TryRemove(networkID, out _);
                    }
                    SendPacketToRoom(net, room, despawnPacket, session.ID);
                    Packet.Pool.Push(despawnPacket);
                }
            }

            void OnChat(GyuNet net, Session session, Packet packet)
            {
                var sessionId = session.ID;
                if (SessionRoomPair.TryGetValue(sessionId, out var room))
                {
                    var chatPacket = Packet.Pool.Pop();
                    lock (room)
                    {
                        chatPacket.Serialize(session.ID);
                        chatPacket.Serialize(packet.DeserializeString());
                        chatPacket.SetHeader((short)PacketHeader.Chat);
                    }
                    SendPacketToRoom(net, room, chatPacket, sessionId);
                    Packet.Pool.Push(chatPacket);
                }
            }

            void OnRpc(GyuNet net, Session session, Packet packet)
            {
                var sessionId = session.ID;
                if (SessionRoomPair.TryGetValue(sessionId, out var room))
                {
                    var rpcPacket = Packet.Pool.Pop();
                    lock (room)
                    {
                        rpcPacket.Serialize(packet.DeserializeInt());
                        rpcPacket.Serialize(packet.DeserializeString());
                        rpcPacket.SetHeader((short)PacketHeader.Rpc);
                    }
                    SendPacketToRoom(net, room, rpcPacket, sessionId);
                    Packet.Pool.Push(rpcPacket);
                }
            }

            void OnRequestSetPlayerObject(GyuNet net, Session session, Packet packet)
            {
                var sessionId = session.ID;
                if (SessionRoomPair.TryGetValue(sessionId, out var room))
                {
                    var setPlayerPacket = Packet.Pool.Pop();
                    lock (room)
                    {
                        var networkObjectId = packet.DeserializeInt();
                        var authority = packet.DeserializeInt();
                        Debug.Log(networkObjectId);
                        Debug.Log(authority);
                        room.PlayerObjects.AddOrUpdate(authority, networkObjectId, (_, __) => networkObjectId);
                        setPlayerPacket.Serialize(networkObjectId);
                        setPlayerPacket.Serialize(authority);
                        setPlayerPacket.SetHeader((short)PacketHeader.SetPlayerObject);
                    }
                    SendPacketToRoom(net, room, setPlayerPacket, session.ID);
                    Packet.Pool.Push(setPlayerPacket);
                }
            }
        }
    }
}