using System.Collections.Concurrent;
namespace GyuNet
{
    public class MemoryPool
    {
        private byte[][] Buffer { get; } = new byte[Define.MAX_CONNECTION][];
        private ConcurrentStack<int> FreeIndex { get; } = new ConcurrentStack<int>();
        private ConcurrentDictionary<byte[], int> ByteIndexDictionary { get; } = new ConcurrentDictionary<byte[], int>();

        public MemoryPool()
        {
            for (int i = 0; i < Buffer.GetLength(0); i++)
            {
                Buffer[i] = new byte[Define.PACKET_SIZE];
                FreeIndex.Push(i);
                ByteIndexDictionary.TryAdd(Buffer[i], i);
            }
        }

        public void Push(byte[] buffer)
        {
            if (ByteIndexDictionary.TryGetValue(buffer, out var index))
            {
                FreeIndex.Push(index);
            }
        }
        
        public byte[] Pop()
        {
            if (FreeIndex.TryPop(out var index) == false)
            {
                Debug.LogError("MemoryPool 내부에 Pop할 아이템이 없습니다.");
                return null;
            }

            return Buffer[index];
        }
    }
}
