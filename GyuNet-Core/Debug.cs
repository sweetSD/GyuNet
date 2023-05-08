using System;
using System.Threading;

namespace GyuNet
{
    public static class Debug
    {
        private static readonly object LockObj = new object();
        private static string CurrentTime => DateTime.Now.ToString();
        private static string DebugInfo => $"[{CurrentTime} | {ThreadId}, {GetThreadPoolInfo()}]";


        private static string GetThreadPoolInfo()
        {
            ThreadPool.GetAvailableThreads(out int workerThreads, out int ioThreads);
            return $"({workerThreads}, {ioThreads})";
        }

        private static int ThreadId => Thread.CurrentThread.ManagedThreadId;

        public static void Log(string message)
        {
            WriteLine($"{DebugInfo} [Log] {message}");
        }

        public static void Log(object obj)
        {
            Log(obj.ToString());
        }
        
        public static void LogWarning(string message)
        {
            WriteLine($"{DebugInfo} [Warning] {message}");
        }

        public static void LogWarning(object obj)
        {
            LogWarning(obj.ToString());
        }
        
        public static void LogError(string message)
        {
            WriteLine($"{DebugInfo} [Error] {message}");
        }

        public static void LogError(object obj)
        {
            LogError(obj.ToString());
        }
        
        public static void LogError(Exception e)
        {
            LogError($"{e.Message}\n{e.StackTrace}");
        }
        
        private static void WriteLine(string message)
        {
            lock (LockObj)
            {
                Console.WriteLine(message);
            }
        }

        public static bool CheckKey(ConsoleKey key)
        {
            lock (LockObj)
            {
                if (Console.KeyAvailable)
                {
                    if (Console.ReadKey().Key == key)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
