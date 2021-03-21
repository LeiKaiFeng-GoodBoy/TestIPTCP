using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LeiKaiFeng.TCPIP
{

    [Flags]
    public enum TCPFlag
    {
        URG = 0b0010_0000,
        ACK = 0b0001_0000,
        PSH = 0b0000_1000,
        RST = 0b0000_0100,
        SYN = 0b0000_0010,
        FIN = 0b0000_0001
    }


    [StructLayout(LayoutKind.Explicit, Size = 2)]
    public struct TCPHeader12_14
    {
        [FieldOffset(0)]
        public byte _Byte_0;


        [FieldOffset(1)]
        public byte _Byte_1;


        public byte HeaderSize => (byte)(_Byte_0 >> 4);


        public TCPFlag TCPFlag => (TCPFlag)_Byte_1;
    }



    [StructLayout(LayoutKind.Explicit, Size = 20)]
    public struct TCPHeader
    {
        public const int HEADER_SIZE = 20;

        [FieldOffset(0)]
        ushort _SourcePort;

        [FieldOffset(2)]
        ushort _DesPort;

        [FieldOffset(4)]
        uint _SequenceNumber;

        [FieldOffset(8)]
        uint _AcknowledgmentNumber;


        [FieldOffset(12)]
        public TCPHeader12_14 _TCPHeader12_14;

        [FieldOffset(14)]
        ushort _WindowSize;

        [FieldOffset(16)]
        ushort _Checksum;


        [FieldOffset(18)]
        ushort _UrgentPointer;



        public ushort SourcePort => Meth.AsBigEndian(_SourcePort);


        public ushort DesPort => Meth.AsBigEndian(_DesPort);

        public uint SequenceNumber => Meth.AsBigEndian(_SequenceNumber);


        public uint AcknowledgmentNumber => Meth.AsBigEndian(_AcknowledgmentNumber);


        public ushort WindowSize => Meth.AsBigEndian(_WindowSize);


        public static void Set(ref TCPHeader header, IPv4Address sourceAddress,      
            ushort sourcePort,
            IPv4Address desAddress,
            ushort desPort,
            uint sm,
            byte[] buffer,
            int offst, 
            int count)
        {
            header = new TCPHeader();

            header._SourcePort = Meth.AsBigEndian(sourcePort);

            header._DesPort = Meth.AsBigEndian(desPort);

            
            header._SequenceNumber = 123543;

            header._AcknowledgmentNumber = Meth.AsBigEndian(sm + 1);


            

            header._WindowSize = Meth.AsBigEndian((ushort)65535);

            header._TCPHeader12_14._Byte_0 = (byte)(5 << 4);

            header._TCPHeader12_14._Byte_1 = (byte)(TCPFlag.RST | TCPFlag.ACK);

            var ph = new PseudoHeader(
                sourceAddress,
                desAddress,
                 Protocol.TCP,
                (ushort)count);

            header._Checksum = Meth.AsBigEndian(Meth.CalculationHeaderChecksum(ph, buffer.AsSpan(offst), count));

        }
    }
}
