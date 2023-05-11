using System;
using System.Collections.Concurrent;
namespace GyuNet
{
    public class Pool<T> where T : class, new()
    {
        public int Capacity { get; set; }
        public bool ShouldExpand { get; set; }
        
        public event Action<T> Spawned;
        public event Action<T> Despawned;
        
        private ConcurrentStack<T> objectStack = new ConcurrentStack<T>();

        public Pool(int capacity = 10, bool shouldExpand = true, Action<T> spawned = null, Action<T> despawned = null)
        {
            Capacity = capacity;
            ShouldExpand = shouldExpand;
            Spawned = spawned;
            Despawned = despawned;

            for (int i = 0; i < Capacity; i++)
            {
                objectStack.Push(new T());
            }
        }
        
        public void Push(T item)
        {
            if (item == null)
            {
                Debug.LogError("Pool 내부에 Push할 아이템이 null입니다.");
                return;
            }
            Despawned?.Invoke(item);
            objectStack.Push(item);
        }
        
        public T Pop()
        {
            T obj;
            
            if (objectStack.IsEmpty)
            {
                Debug.LogError("Pool 내부에 Pop할 아이템이 없습니다.");
                if (ShouldExpand == false)
                    return null;
                obj = new T();
            }
            else if (objectStack.TryPop(out obj) == false)
            {
                Debug.LogError("Pool 내부에 Pop하는 중 예외 발생.");
                return null;
            }
            
            Spawned?.Invoke(obj);
            return obj;
        }
    }
}
