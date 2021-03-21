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

namespace TestIPTCP
{

    class Program
    {

        static void UdpRead(Socket socket)
        {

            var la = IPLayer.Init(new IPLayerInfo(
                (buffer, offst, count) => socket.Receive(buffer, offst, count, SocketFlags.None),
                (buffer, offset, count) => socket.Send(buffer, offset, count, SocketFlags.None)));

            while (true)
            {

                var packet = la.TakeUPPacket();


                var header = packet.TCPHeader();


                Console.WriteLine($"{header.DesPort} {header.WindowSize} {header._TCPHeader12_14.HeaderSize} {header._TCPHeader12_14.TCPFlag}"); ;

            }
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
