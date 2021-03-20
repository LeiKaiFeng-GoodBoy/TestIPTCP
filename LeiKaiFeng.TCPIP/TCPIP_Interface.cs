using System;

namespace LeiKaiFeng.TCPIP
{
    public interface IIPUpProtocol
    {
        IPWriteData WriteIPPacket(Span<byte> buffer);


        void ReadIPPacket(IPReadData  readData);
    }
}