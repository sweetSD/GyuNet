using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GyuNet
{
    class Program
    {
        static void Main(string[] args)
        {
            TcpGyuNet tcpGyuNet = new TcpGyuNet();
            tcpGyuNet.Start();
            
            Debug.Log("서버 시작. esc 혹은 q를 눌러 종료");
            Debug.Log($">> {GyuNetUtility.GetIP()}:{Define.TCP_PORT}");
            
            while (tcpGyuNet.IsRunning)
            {
                if (Debug.CheckKey(ConsoleKey.Escape))
                {
                    tcpGyuNet.Stop();
                }
            }
            
            Debug.Log("서버 종료. 아무 키나 눌러 종료하세요.");
            Console.ReadKey();
        }
    }
}
