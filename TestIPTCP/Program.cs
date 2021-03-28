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

        static void Udp()
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            socket.Bind(new IPEndPoint(IPAddress.Parse("192.168.1.106"), 5050));

            socket.Connect(new IPEndPoint(IPAddress.Parse("192.168.1.104"), 5050));

            //\\.\pipe\MMYY
            var wir = WiresharkSender.Create("MMYY");

            TCPLayerInfo layerInfo = new TCPLayerInfo((tcp) =>
            {
                Socket connect = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                connect.Connect(new IPEndPoint(IPAddress.Loopback, 80));

                NetworkStream networkStream = new NetworkStream(connect);


                networkStream.CopyToAsync(tcp);

                tcp.CopyToAsync(networkStream);


            });

            var tcp = TCPLayer.Init(layerInfo);


            var la = IPLayer.Init(new IPLayerInfo(
                ReadUDPAndWir(socket, wir),
                WriteUDPAndWir(socket, wir),
                tcp.UPPacket,
                tcp.DownPacket), (e) => Console.WriteLine(e));



            Console.Read();
        }

        static void Main()
        {
            Udp();
        }
    }
}