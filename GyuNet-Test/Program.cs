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
        static async Task Main(string[] args)
        {
            await Task.Delay(5000);
            
            TcpClient client = new TcpClient(AddressFamily.InterNetwork);
            client.Connect(new IPEndPoint(IPAddress.Loopback, 8000));

            Packet packet = new Packet();
            packet.ResetPosition();
            packet.Serialize(false);
            packet.Serialize(1);
            packet.Serialize(5);
            packet.Serialize("Hello");

            packet.SetHeader(PacketHeader.PING);
            
            Debug.Log(packet.ReadOffset);
            Debug.Log(packet.WriteOffset);

            client.GetStream().Write(packet.Buffer, 0, packet.WriteOffset);

            client.Close();
        }
    }
}
