namespace GyuNet
{
    public static class Define
    {
        public const int MAX_CONNECTION = 3000;
        
        public const int TCP_PORT = 8000;
        public const int UDP_PORT = 8001;
        
        public const int HEADER_SIZE = sizeof(PacketHeader) + sizeof(int);
        public const int PACKET_SIZE = 1024;
        public const int BUFFER_SIZE = PACKET_SIZE * 4;
    }
}
