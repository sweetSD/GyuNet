using System.Net.Sockets;
namespace GyuNet
{
    public static class GyuNetPool
    {
        public static readonly Pool<UserSession> Sessions = new Pool<UserSession>();
        public static readonly Pool<SocketAsyncEventArgs> EventArgs = new Pool<SocketAsyncEventArgs>();
        public static readonly MemoryPool Memories = new MemoryPool();
    }
}
