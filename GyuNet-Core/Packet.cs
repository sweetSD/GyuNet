﻿using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace GyuNet
{
    [Serializable]
    public class Packet
    {
        public static readonly Pool<Packet> Pool = new Pool<Packet>();
        
        public short Header { get; set; }
        
        private int writeOffset;
        public int WriteOffset
        {
            get => writeOffset;
            private set => writeOffset = value;
        }
        
        private int readOffset;
        public int ReadOffset
        {
            get => readOffset;
            private set => readOffset = value;
        }

        public byte[] Buffer { get; private set; } = new byte[Define.PACKET_SIZE];

        static Packet()
        {
            Pool.Spawned += (packet) => packet.ResetPosition();
        }
        
        public Packet()
        {
            ResetPosition();
        }

        public void ResetPosition(int? wOffset = null, int? rOffset = null)
        {
            // 버퍼의 제일 첫 부분은 헤더 + 바디 사이즈 데이터가 들어감
            WriteOffset = wOffset ?? Define.HEADER_SIZE;
            ReadOffset = rOffset ?? 0;
        }

        public void CopyBuffer(byte[] buf, int offset, int size)
        {
            // 버퍼가 지정될 경우 
            if (buf != null)
            {
                System.Buffer.BlockCopy(buf, offset, Buffer, WriteOffset, size);
                WriteOffset += size;
            }
        }

        // 네트워크로 보내기 위한 정보를 저장합니다. 헤더만 바꿀 경우 Header 프로퍼티를 이용하세요.
        public void SetHeader(short header)
        {
            Header = header;
            var headerSize = sizeof(short);
            var headerBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(header));
            Array.Copy(headerBytes, 0, Buffer, 0, headerSize);

            var bodySize = sizeof(int);
            var bodySizeBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(WriteOffset));
            Array.Copy(bodySizeBytes, 0, Buffer, headerSize, bodySize);
        }
        
        public void Serialize(bool value)
        {
            var size = sizeof(bool);
            
            if (CanWrite(size) == false)
            {
                throw new Exception("버퍼에 공간이 부족합니다.");
            }
            
            var bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, Buffer, WriteOffset, size);
            WriteOffset += size;
        }
        
        public void Serialize(short value)
        {
            var size = sizeof(short);
            
            if (CanWrite(size) == false)
            {
                throw new Exception("버퍼에 공간이 부족합니다.");
            }
            
            var bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            Array.Copy(bytes, 0, Buffer, WriteOffset, size);
            WriteOffset += size;
        }
        
        public void Serialize(int value)
        {
            var size = sizeof(int);
            
            if (CanWrite(size) == false)
            {
                Debug.LogError("버퍼에 공간이 부족합니다.");
            }
            
            var bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            Array.Copy(bytes, 0, Buffer, WriteOffset, size);
            WriteOffset += size;
        }
        
        public void Serialize(uint value)
        {
            var size = sizeof(int);
            
            if (CanWrite(size) == false)
            {
                throw new Exception("버퍼에 공간이 부족합니다.");
            }

            var bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(unchecked((int)value)));
            Array.Copy(bytes, 0, Buffer, WriteOffset, size);
            WriteOffset += size;
        }
        
        public void Serialize(float value)
        {
            var size = sizeof(float);
            
            if (CanWrite(size) == false)
            {
                throw new Exception("버퍼에 공간이 부족합니다.");
            }
            
            var bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, Buffer, WriteOffset, size);
            WriteOffset += size;
        }
        
        public void Serialize(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var size = bytes.Length;
            Serialize(size);
            
            if (CanWrite(size) == false)
            {
                throw new Exception("버퍼에 공간이 부족합니다.");
            }
            
            Array.Copy(bytes, 0, Buffer, WriteOffset, size);
            WriteOffset += size;
        }
        
        public void Serialize((float x, float y) value)
        {
            Serialize(value.x);
            Serialize(value.y);
        }
        
        public void Serialize((float x, float y, float z) value)
        {
            Serialize(value.x);
            Serialize(value.y);
            Serialize(value.z);
        }
        
        public void Serialize<T>(T value) where T : struct
        {
            var size = Marshal.SizeOf<T>();
            
            if (CanWrite(size) == false)
            {
                throw new Exception("버퍼에 공간이 부족합니다.");
            }
            
            var bytes = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(value, ptr, true);
            Marshal.Copy(ptr, bytes, 0, size);
            Marshal.FreeHGlobal(ptr);

            Array.Copy(bytes, 0, Buffer, WriteOffset, size);
            WriteOffset += size;
        }

        public bool DeserializeBool()
        {
            var size = sizeof(bool);
            if (CanRead(size) == false)
            {
                throw new Exception(
                    $"버퍼의 모든 데이터를 읽었습니다. Read: {ReadOffset} | Write: {WriteOffset} | Deserialize Size: {size}");
            }
            var value = BitConverter.ToBoolean(Buffer, ReadOffset);
            ReadOffset += size;
            return value;
        }
        
        public short DeserializeShort()
        {
            var size = sizeof(short);
            if (CanRead(size) == false)
            {
                throw new Exception(
                    $"버퍼의 모든 데이터를 읽었습니다. Read: {ReadOffset} | Write: {WriteOffset} | Deserialize Size: {size}");
            }
            var value = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(Buffer, ReadOffset));
            ReadOffset += size;
            return value;
        }
        
        public int DeserializeInt()
        {
            var size = sizeof(int);
            if (CanRead(size) == false)
            {
                throw new Exception(
                    $"버퍼의 모든 데이터를 읽었습니다. Read: {ReadOffset} | Write: {WriteOffset} | Deserialize Size: {size}");
            }
            var value = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(Buffer, ReadOffset));
            ReadOffset += size;
            return value;
        }
        
        public uint DeserializeUInt()
        {
            var size = sizeof(int);
            if (CanRead(size) == false)
            {
                throw new Exception(
                    $"버퍼의 모든 데이터를 읽었습니다. Read: {ReadOffset} | Write: {WriteOffset} | Deserialize Size: {size}");
            }
            var value = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(Buffer, ReadOffset));
            ReadOffset += size;
            return unchecked((uint)value);
        }
        
        public float DeserializeFloat()
        {
            var size = sizeof(float);
            if (CanRead(size) == false)
            {
                throw new Exception(
                    $"버퍼의 모든 데이터를 읽었습니다. Read: {ReadOffset} | Write: {WriteOffset} | Deserialize Size: {size}");
            }
            var value = BitConverter.ToSingle(Buffer, ReadOffset);
            ReadOffset += size;
            return value;
        }
        
        public string DeserializeString()
        {
            var size = DeserializeInt();
            if (CanRead(size) == false)
            {
                throw new Exception(
                    $"버퍼의 모든 데이터를 읽었습니다. Read: {ReadOffset} | Write: {WriteOffset} | Deserialize Size: {size}");
            }
            var value = Encoding.UTF8.GetString(Buffer, ReadOffset, size);
            ReadOffset += size;
            return value;
        }
        
        public (float x, float y) DeserializeVector2()
        {
            return (DeserializeFloat(), DeserializeFloat());
        }
        
        public (float x, float y, float z) DeserializeVector3()
        {
            return (DeserializeFloat(), DeserializeFloat(), DeserializeFloat());
        }

        public bool Deserialize<T>(out T val) where T : struct
        {
            var size = Marshal.SizeOf<T>();
            
            if (CanRead(size) == false)
            {
                throw new Exception(
                    $"버퍼의 모든 데이터를 읽었습니다. Read: {ReadOffset} | Write: {WriteOffset} | Deserialize Size: {size}");
            }

            var bytes = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);
            Marshal.Copy(bytes, 0, ptr, size);
            val = Marshal.PtrToStructure<T>(ptr);
            Marshal.FreeHGlobal(ptr);

            WriteOffset += size;
            return true;
        }

        private bool CanWrite(int size)
        {
            return WriteOffset + size <= Define.PACKET_SIZE;
        }
        
        private bool CanRead(int size)
        {
            return ReadOffset + size <= WriteOffset;
        }
    }
}
