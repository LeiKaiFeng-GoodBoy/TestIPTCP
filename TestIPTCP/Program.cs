using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Linq;
using LeiKaiFeng.TCPIP;
using System.Text;
using System.Threading;
using Wireshark;

namespace TestIPTCP
{

    class Program
    {

        static void Tcp()
        {
            Socket listen = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listen.Bind(new IPEndPoint(IPAddress.Loopback, 80));

            listen.Listen(0);

            listen.ReceiveBufferSize = 4096;
            Socket connect = listen.Accept();


            Console.ReadLine();


        }
        static int WriteDes(byte[] buffer)
        {
            Span<byte> span = stackalloc byte[] {
                0x48,
                0x7d,
                0x2e,
                0x81,
                0xd1,
                0x37,
                0x00,
                0x08,
                0xca,
                0xc1,
                0x87,
                0xa9,
                0x08,
                0x00
            };


            span.CopyTo(buffer);


            return span.Length;
        }


        static int WriteSource(byte[] buffer)
        {
            Span<byte> span = stackalloc byte[] {
                0x00,
                0x08,
                0xca,
                0xc1,
                0x87,
                0xa9,
                0x48,
                0x7d,
                0x2e,
                0x81,
                0xd1,
                0x37,
                0x08,
                0x00
            };

            span.CopyTo(buffer);

            return span.Length;
        }

        static Action<byte[], int,int> WriteUDPAndWir(Socket socket, WiresharkSender sender)
        {
            byte[] readArray = new byte[75536];

            int index = WriteDes(readArray);


            return (buffer, offset, count) =>
            {
                buffer.AsSpan(offset, count).CopyTo(readArray.AsSpan(index));



                sender.SendToWireshark(readArray, 0, index + count);


                socket.Send(readArray, index, count, SocketFlags.None);

            };
        }

        static Func<byte[], int, int, int> ReadUDPAndWir(Socket socket, WiresharkSender sender)
        {
            
            byte[] readArray = new byte[75536];

            int index = WriteSource(readArray);


            return (buffer, offset, count) =>
            {
                int n = socket.Receive(readArray, index, readArray.Length - index, SocketFlags.None);


                sender.SendToWireshark(readArray, 0, n + index);

                readArray.AsSpan(index, n).CopyTo(buffer.AsSpan(offset, count));

                return n;
            };
        }

        static void UdpRead(Socket socket)
        {
            //\\.\pipe\bacnet
            var wir = WiresharkSender.Create("MMYY");

            var la = IPLayer.Init(new IPLayerInfo(
                ReadUDPAndWir(socket, wir),
                WriteUDPAndWir(socket, wir)));


            TCPLayerInfo layerInfo = new TCPLayerInfo(la, (tcp) => Console.WriteLine(tcp.Quaternion));

            var tcp = TCPLayer.Init(layerInfo, (e) => Console.WriteLine(e));

            Console.Read();

        }


        static void UdpWrite(Socket socket)
        {
            byte[] buffer = new byte[75536];

            var stream = new IPProtocol(new UDPProtocol());
            while (true)
            {

                int n = socket.Receive(buffer, SocketFlags.None);

                stream.WritePacket(buffer);

                Thread.Sleep(100);

            }
        }

        static void Udp()
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);



            socket.Bind(new IPEndPoint(IPAddress.Parse("192.168.1.106"), 5050));

            socket.Connect(new IPEndPoint(IPAddress.Parse("192.168.1.105"), 5050));

            UdpRead(socket);
        }

        static void Raw()
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);


            socket.Bind(new IPEndPoint(IPAddress.Parse("192.168.1.106"), 5050));

            IPLayer layer = IPLayer.Init(new IPLayerInfo(
                (buffer, offset, count) => socket.Receive(buffer, offset, count, SocketFlags.None),
                (buffer, offset, count) => socket.Send(buffer, offset, count, SocketFlags.None)));
            while (true)
            {

                var up = layer.TakeUPPacket();

                Console.WriteLine($"{up.IPData.SourceAddress} {up.IPData.DesAddress} {up.IPData.Protocol}");

            }
        }

        static void Main()
        {
            Udp();
        }
    }
}
