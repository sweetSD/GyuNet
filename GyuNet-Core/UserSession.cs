using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
namespace GyuNet
{
    public class UserSession
    {
        public Socket Socket;
        
        private byte[] buffer = new byte[Define.BUFFER_SIZE];
        private int offset = 0;
        
        public ConcurrentQueue<Packet> ReceivedPacketQueue { get; } = new ConcurrentQueue<Packet>();
        public ConcurrentQueue<Packet> SendPacketQueue { get; } = new ConcurrentQueue<Packet>();

        public void OnPacketProcess(byte[] buf, int transferred)
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
                    var packet = new Packet();
                    packet.Header = header;
                    
                    packet.CopyBuffer(buffer, 0, bodySize);
                    ReceivedPacketQueue.Enqueue(packet);
                    
                    Buffer.BlockCopy(buffer, bodySize, buffer, 0, offset - bodySize);
                    offset -= bodySize;

                    Debug.Log("패킷 완성!");
                    Debug.Log(packet.Header);
                }
            }
        }
    }
}
