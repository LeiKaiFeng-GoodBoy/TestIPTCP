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
            byte[] buffer = new byte[75536];

            var stream = new IPProtocol(new UDPProtocol());
            while (true)
            {

                int n = stream.ReadPacket(buffer);
                Console.WriteLine(n);
                socket.Send(buffer, 0, n, SocketFlags.None);

                Thread.Sleep(1000);
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

            byte[] buffer = new byte[65536];

            var stream = new IPProtocol(new UDPProtocol());
            while (true)
            {
                int n = socket.Receive(buffer);

                stream.WritePacket(buffer);

                Thread.Sleep(1000);
            }
        }

        static void Main()
        {
            Udp();
        }
    }
}
