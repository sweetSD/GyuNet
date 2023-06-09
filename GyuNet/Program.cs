using System;
using System.Collections.Concurrent;
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
            Unity.Unity unity = new Unity.Unity();
            unity.Start();

            Debug.Log("서버 시작. esc 혹은 q를 눌러 종료");
            Debug.Log($">> {GyuNetUtility.GetIP()}:{Define.TCP_PORT}");


            while (unity.IsRunning)
            {
                if (Debug.CheckKey(ConsoleKey.Escape))
                {
                    unity.Stop();
                }
            }

            Debug.Log("서버 종료. 아무 키나 눌러 종료하세요.");
            Console.ReadKey();
        }
    }
}
