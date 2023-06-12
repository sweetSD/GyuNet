namespace GyuNet
{
    public static class Define
    {
        // TCP Settings ==============================
        public const int MAX_CONNECTION = 3000;
        
        public const int TCP_PORT = 8000;
        public const int UDP_PORT = 8001;

        // Buffer Settings ===========================
        public const int HEADER_SIZE = sizeof(short) + sizeof(int);
        public const int PACKET_SIZE = 1024 * 4;
        public const int BUFFER_SIZE = PACKET_SIZE * 16;
        
        // MySQL Settings ============================
        public const string MYSQL_SERVER = "localhost";
        public const string MYSQL_UID = "gyunet";
        public const string MYSQL_PASSWORD = "gyunet";
        public const string MYSQL_DATABASE = "ckgame";
    }
}
