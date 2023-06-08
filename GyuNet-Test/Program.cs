using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using GyuNet;

namespace GyuNet_Test
{
    class Program
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
        
        static async Task Main(string[] args)
        {
            await Task.Delay(5000);
            
            // TCP
            
            // TcpClient tcpClient = new TcpClient(AddressFamily.InterNetwork);
            // tcpClient.Connect(new IPEndPoint(IPAddress.Loopback, Define.TCP_PORT));
            //
            // Packet tcpPacket = new Packet();
            // tcpPacket.ResetPosition();
            // tcpPacket.Serialize(false);
            // tcpPacket.Serialize(1);
            // tcpPacket.Serialize(5);
            // tcpPacket.Serialize("Hello");
            //
            // tcpPacket.SetHeader(PacketHeader.PING);
            //
            // Debug.Log(tcpPacket.ReadOffset);
            // Debug.Log(tcpPacket.WriteOffset);
            //
            // tcpClient.GetStream().Write(tcpPacket.Buffer, 0, tcpPacket.WriteOffset);
            // await Task.Delay(3000);
            // tcpClient.Close();
            
            // UDP

            UdpClient udpClient = new UdpClient(AddressFamily.InterNetwork);
            udpClient.Connect(new IPEndPoint(IPAddress.Loopback, Define.UDP_PORT));

            Packet udpPacket = new Packet();
            udpPacket.ResetPosition();
            udpPacket.Serialize(true);
            udpPacket.Serialize(5);
            udpPacket.Serialize(12745);
            udpPacket.Serialize("World!");

            udpPacket.SetHeader((short)PacketHeader.Ping);
            
            Debug.Log(udpPacket.ReadOffset);
            Debug.Log(udpPacket.WriteOffset);

            await udpClient.SendAsync(udpPacket.Buffer, udpPacket.WriteOffset);
            await Task.Delay(3000);
            udpClient.Close();
        }
    }
}
