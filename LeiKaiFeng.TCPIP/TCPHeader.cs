using System.Runtime.InteropServices;

namespace LeiKaiFeng.TCPIP
{

    [StructLayout(LayoutKind.Explicit, Size = 2)]
    public struct TCPHeader12_14
    {
        [FieldOffset(0)]
        ushort _Value;
    }



    [StructLayout(LayoutKind.Explicit, Size = 20)]
    public struct TCPHeader
    {
        [FieldOffset(0)]
        ushort _SourcePort;

        [FieldOffset(2)]
        ushort _DesPort;

        [FieldOffset(4)]
        uint _Index;

        [FieldOffset(8)]
        uint _OKIndex;


        [FieldOffset(12)]
        TCPHeader12_14 _TCPHeader12_14;

        [FieldOffset(14)]
        ushort _WindowSize;

        [FieldOffset(16)]
        ushort _CuckSum;


        [FieldOffset(18)]
        ushort _Point;



        public ushort SourcePort => Meth.AsBigEndian(_SourcePort);


        public ushort DesPort => Meth.AsBigEndian(_DesPort);

        public uint Index => Meth.AsBigEndian(_Index);


        public uint KOIndex => Meth.AsBigEndian(_OKIndex);


        public ushort WinssizeSoze => Meth.AsBigEndian(_WindowSize);
    }
}
