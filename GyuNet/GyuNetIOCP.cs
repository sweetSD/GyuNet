using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GyuNet
{
    public class IOCPSocket
    {
        public object Lock = new object();
        public Socket Socket;
        public bool Disconnected = false;

        public void Reset(Socket socket)
        {
            Socket = socket;
            Disconnected = false;
        }
    }

    public class GyuNetIOCP
    {   
        private readonly ConcurrentDictionary<Socket, IOCPSocket> socketDictionary = new ConcurrentDictionary<Socket, IOCPSocket>();
        private readonly CancellationToken terminateToken;

        public ConcurrentQueue<Packet> ReceivedPacketQueue { get; private set; } = new ConcurrentQueue<Packet>();
        public ConcurrentQueue<Packet> SendPacketQueue { get; private set; } = new ConcurrentQueue<Packet>();

        public GyuNetIOCP(CancellationToken token)
        {
            terminateToken = token;
            Task.Run(Update, terminateToken);
        }
        
        public void AddClient(Socket client)
        {
            // 추후 Object Pool 사용하는걸로 변경
            var socket = new IOCPSocket();
            socket.Reset(client);

            socketDictionary.TryAdd(client, socket);

            Task.Factory.StartNew(ClientTask, socket, terminateToken);
        }

        public void RemoveClient(Socket client)
        {
            if (socketDictionary.TryGetValue(client, out var socket))
            {
                socket.Disconnected = true;
            }
            else
            {
                Debug.LogWarning("해당하는 IOCP Socket을 찾을 수 없습니다.");
            }
        }

        private async void Update()
        {
            while (true)
            {
                await Task.Yield();
            }
        }

        private async void ClientTask(object param)
        {
            var client = param as IOCPSocket;

            if (client == null)
            {
                Debug.LogError("IOCP 소켓이 Null 입니다.");
                return;
            }
            
            byte[] buffer = new byte[Define.BUFFER_SIZE];

            try
            {
                while (client.Disconnected == false)
                {
                    var headerReadSize = await ReadNBytes(client.Socket, buffer, Define.HEADER_SIZE);

                    if (headerReadSize == 0 || client.Disconnected)
                    {
                        client.Disconnected = true;
                        break;
                    }

                    Packet packet = new Packet();
                    packet.ResetPosition(headerReadSize, 0);
                    packet.CopyBuffer(buffer, 0, headerReadSize);

                    var header = (PacketHeader)packet.DeserializeShort();
                    var bodySize = packet.DeserializeInt();
                    
                    packet.Header = header;

                    var bodyReadSize = await ReadNBytes(client.Socket, buffer, bodySize);

                    if (bodyReadSize == 0 || client.Disconnected)
                    {
                        client.Disconnected = true;
                        break;
                    }

                    
                    packet.ResetPosition(headerReadSize + bodyReadSize, headerReadSize);
                    packet.CopyBuffer(buffer, headerReadSize, bodyReadSize);
                    
                    ReceivedPacketQueue.Enqueue(packet);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                Debug.Log($"클라이언트 접속 종료. {client.Socket.RemoteEndPoint}");
                client.Socket?.Close();
            }
        }
        
        static async Task<int> ReadNBytes(Socket socket, byte[] buffer, int length)
        {
            int offset = 0;
            int readBytesSizeLeft = length;
            while (readBytesSizeLeft > 0)
            {
                try
                {
                    SocketAsyncEventArgs eventArgs = new SocketAsyncEventArgs();
                    eventArgs.Completed += (sender, args) =>
                    {

                    };
                    var readBytesSize = await socket.ReceiveAsync(buffer, offset, readBytesSizeLeft);
                    if (readBytesSize == 0) break;
                    readBytesSizeLeft -= readBytesSize;
                    offset += readBytesSize;
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }
            return length - readBytesSizeLeft;
        }
    }
}
