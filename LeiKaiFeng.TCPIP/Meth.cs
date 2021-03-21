using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Linq;

namespace LeiKaiFeng.TCPIP
{
    static class Meth
    {

        static int s_count = 1234567;

        public static ushort GetCount()
        {
            return (ushort)(++s_count);
        }


        public static void GetOffsetCount(byte[] buffer, Span<byte> span, out int offset)
        {

            offset = buffer.Length - span.Length;
        }


        //这个地方可能不够转换为一个结构
        public static ref T AsStruct<T>(Span<byte> buffer) where T : unmanaged
        {
            return ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, T>(buffer));
        }

        public static T AsBigEndian<T>(T value) where T : unmanaged
        {
           
            if (BitConverter.IsLittleEndian)
            {
                return ReverseEndianness(value);
            }
            else
            {
                return value;
            }
        }

        static T ReverseEndianness<T>(T value) where T : unmanaged
        {
            unsafe
            {
                fixed (byte* p = &Unsafe.As<T, byte>(ref value))
                {
                    int size = Unsafe.SizeOf<T>();
                    
                    (new Span<byte>(p, size)).Reverse();
                }
            }

            return value;
        }


        //从网上抄的最清晰的实现
        //public static ushort ComputeHeaderIpChecksum(byte[] header, int start, int length)
        //{

        //    ushort word16;
        //    long sum = 0;
        //    for (int i = start; i < (length + start); i += 2)
        //    {
        //        word16 = (ushort)(((header[i] << 8) & 0xFF00)
        //        + (header[i + 1] & 0xFF));
        //        sum += (long)word16;
        //    }

        //    while ((sum >> 16) != 0)
        //    {
        //        sum = (sum & 0xFFFF) + (sum >> 16);
        //    }

        //    sum = ~sum;

        //    return (ushort)sum;


        //}

        static uint AddStructSum<T>(ref T value) where T : unmanaged
        {
            unsafe
            {
                fixed (byte* p = &Unsafe.As<T, byte>(ref value))
                {
                    int size = Unsafe.SizeOf<T>();

                    return AddSpanBytesSum(new Span<byte>(p, size));

                }
            }
        }

        //这个地方必须保证不为奇数长度
        //这个地方在于求和要按照网络字节序
        //求出的结果是按照本机字节序解释的，写入还要转换
        //假如是校验的话，结果是零的话不论字节序如何应该都一样？
        static uint AddSpanBytesSum(Span<byte> buffer)
        {
            var vs = MemoryMarshal.Cast<byte, ushort>(buffer);

            uint sum = 0;

            foreach (var item in vs)
            {
                sum += Meth.AsBigEndian(item);
            }

            if ((buffer.Length % sizeof(ushort)) != 0)
            {
              
                uint n = buffer[buffer.Length - 1];

                sum += n << 8;
            }


            return sum;
        }



        //计算和的二进制补码？
        static ushort AsSum(uint sum)
        {
            const int LEFT_MOVE_COUNT = 16;


            while ((sum >> LEFT_MOVE_COUNT) != 0)
            {
                sum = (sum & 0xFFFF) + (sum >> LEFT_MOVE_COUNT);
            }

            sum = ~sum;

            return (ushort)sum;
        }

        public static ushort CalculationHeaderChecksum(ref IPHeader header)
        {
            uint sum = AddStructSum(ref header);

            return AsSum(sum);
        }

        
        public static ushort CalculationHeaderChecksum(PseudoHeader pseudoHeader, Span<byte> buffer)
        {
            uint sum = AddStructSum(ref pseudoHeader);

            sum += AddSpanBytesSum(buffer);

            return AsSum(sum);
        }
    }
}