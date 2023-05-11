using System.Net.Sockets;
namespace GyuNet
{
    public static class GyuNetPool
    {
        public static readonly Pool<Session> Sessions = new Pool<Session>();
        public static readonly Pool<SocketAsyncEventArgs> EventArgs = new Pool<SocketAsyncEventArgs>();
        public static readonly MemoryPool Memories = new MemoryPool();
    }
}
