using System.Net;
using System.Net.Sockets;
namespace GyuNet
{
    public class GyuNetUtility
    {
        public static string GetIP()
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var address in host.AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                    return address.MapToIPv4().ToString();
            }
            return "no actived internetwork in this system";
        }
    }
}
