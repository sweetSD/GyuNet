using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace GyuNet
{
    public class Session
    {
        public bool Connected { get; set; }
        public int ID { get; set; }
        public object UserData { get; set; }

        protected byte[] ReceiveBuffer { get; } = new byte[Define.BUFFER_SIZE];
        protected int ReceiveOffset { get; set; } = 0;
        
        public ConcurrentQueue<Packet> ReceivedPacketQueue { get; } = new ConcurrentQueue<Packet>();
        public ConcurrentQueue<Packet> SendPacketQueue { get; } = new ConcurrentQueue<Packet>();
        
        public void ReceiveData(byte[] buffer, int count)
        {
            if (count == 0)
                return;
            Buffer.BlockCopy(buffer, 0, ReceiveBuffer, ReceiveOffset, count);
            ReceiveOffset += count;

            while (ReceiveOffset >= Define.HEADER_SIZE)
            {
                // 헤더 확인
                var header = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(ReceiveBuffer, 0));
                var bodySize = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(ReceiveBuffer, sizeof(short)));

                if (ReceiveOffset >= bodySize)
                {
                    var packet = Packet.Pool.Pop();
                    packet.ResetPosition(0, Define.HEADER_SIZE);
                    packet.Header = header;

                    packet.CopyBuffer(ReceiveBuffer, 0, bodySize);

                    Buffer.BlockCopy(ReceiveBuffer, bodySize, ReceiveBuffer, 0, ReceiveOffset - bodySize);
                    ReceiveOffset -= bodySize;
                    ReceivedPacketQueue.Enqueue(packet);
                }
                else break;
            }
        }
    }
    
    public class TCPSession : Session
    {
        public static readonly Pool<TCPSession> Pool = new Pool<TCPSession>();
        
        public Socket Socket;
    }
    
    public class UDPSession : Session
    {
        public static readonly Pool<UDPSession> Pool = new Pool<UDPSession>();
        
        public EndPoint EndPoint;
    }
}
