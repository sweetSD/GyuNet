using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace GyuNet
{
    public class UserSession
    {
        public static readonly Pool<UserSession> Pool = new Pool<UserSession>();
        
        public Socket Socket;
        public uint ID;
        public object UserData;
        
        private readonly byte[] buffer = new byte[Define.BUFFER_SIZE];
        private int offset = 0;
        
        public ConcurrentQueue<Packet> ReceivedPacketQueue { get; } = new ConcurrentQueue<Packet>();
        public ConcurrentQueue<Packet> SendPacketQueue { get; } = new ConcurrentQueue<Packet>();

        public void SendPacket(PacketHeader header, params object[] values)
        {
            Packet packet = Packet.Pool.Pop();
            foreach(var value in values)
            {
                if (value is bool boolValue)
                    packet.Serialize(boolValue);
                else if (value is short shortValue)
                    packet.Serialize(shortValue);
                else if (value is int intValue)
                    packet.Serialize(intValue);
                else if (value is float floatValue)
                    packet.Serialize(floatValue);
                else if (value is string stringValue)
                    packet.Serialize(stringValue);
            }
            packet.SetHeader(header);
            SendPacketQueue.Enqueue(packet);
        }

        public void SendPacket(PacketHeader header, Action<Packet> serialize)
        {
            var packet = Packet.Pool.Pop();
            serialize?.Invoke(packet);
            packet.SetHeader(header);
            SendPacketQueue.Enqueue(packet);
        }
        
        public void ReceivePacket(byte[] buf, int transferred)
        {
            Buffer.BlockCopy(buf, 0, buffer, offset, transferred);
            offset += transferred;

            if (offset >= Define.HEADER_SIZE)
            {
                // 헤더 확인
                var header = (PacketHeader)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(buffer, 0));
                var bodySize = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, sizeof(PacketHeader)));
                
                if (offset >= bodySize)
                {
                    var packet = Packet.Pool.Pop();
                    packet.ResetPosition(0, Define.HEADER_SIZE);
                    packet.Header = header;
                    
                    packet.CopyBuffer(buffer, 0, bodySize);
                    
                    Buffer.BlockCopy(buffer, bodySize, buffer, 0, offset - bodySize);
                    offset -= bodySize;
                    ReceivedPacketQueue.Enqueue(packet);
                }
            }
        }
    }
}
