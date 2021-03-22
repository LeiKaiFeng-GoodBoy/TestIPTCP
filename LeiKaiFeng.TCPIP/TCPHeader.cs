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
        TCPHeader12_14 _TCPHeader12_14;

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

        public byte HeaderSize => (byte)(_TCPHeader12_14._Byte_0 >> 4);

        public TCPFlag TCPFlag => (TCPFlag)_TCPHeader12_14._Byte_1;

        public int AllHeaderSize => HeaderSize * 4;

        public static void Set(
            ref TCPHeader header,
            IPv4Address sourceAddress,      
            ushort sourcePort,
            IPv4Address desAddress,
            ushort desPort,
            TCPFlag tcpFlag,
            ushort windowSize,
            uint sequenceNumber,
            uint acknowledgmentNumber,
            Span<byte> headerAndData)
        {
            header = new TCPHeader();

            header._SourcePort = Meth.AsBigEndian(sourcePort);

            header._DesPort = Meth.AsBigEndian(desPort);

            header._SequenceNumber = Meth.AsBigEndian(sequenceNumber);

            header._AcknowledgmentNumber = Meth.AsBigEndian(acknowledgmentNumber);

            header._WindowSize = Meth.AsBigEndian(windowSize);

            header._TCPHeader12_14._Byte_0 = (byte)(5 << 4);

            header._TCPHeader12_14._Byte_1 = (byte)tcpFlag;

            var ph = new PseudoHeader(
                sourceAddress,
                desAddress,
                Protocol.TCP,
                (ushort)headerAndData.Length);

            header._Checksum = Meth.AsBigEndian(Meth.CalculationHeaderChecksum(ph, headerAndData));
        }
    }
}
