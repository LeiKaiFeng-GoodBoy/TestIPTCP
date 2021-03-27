using DNS.Server;
using LeiKaiFeng.TCPIP;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Test
{
    class Program
    {
        static void SendTcp()
        {
            Socket connect = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            connect.Connect(new IPEndPoint(IPAddress.Loopback, 80));

            while (true)
            {
                connect.Send(new byte[65536]);
            }
        }

        static void SendUdp()
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            socket.Connect(new IPEndPoint(IPAddress.Parse("192.168.1.106"), 5050));


            string[] vs = new string[]
            {
                "0",
                "中国",
                "今天天气晴",
                "车了个发,shujleyijiao aaaaaaa "
            };

            foreach (var item in vs)
            {
                socket.Send(Encoding.UTF8.GetBytes(item));
            }
        }

        static void FFFF()
        {

            uint n = 0;

            int co = ushort.MaxValue + 2;

            for (int i = 0; i < co; i++)
            {

                n = checked(n + ushort.MaxValue);





            }

            Console.WriteLine(n);

        }

        static void F()
        {
            // Proxy to google's DNS
            MasterFile masterFile = new MasterFile();
            DnsServer server = new DnsServer(masterFile, "8.8.8.8");

            // Resolve these domain to localhost
            masterFile.AddIPAddressResourceRecord("google.com", "127.0.0.1");
            masterFile.AddIPAddressResourceRecord("github.com", "127.0.0.1");

            // Log every request
            server.Requested += (sender, e) => Console.WriteLine(e.Request);
            // On every successful request log the request and the response
            server.Responded += (sender, e) => Console.WriteLine("{0} => {1}", e.Request, e.Response);
            // Log errors
            server.Errored += (sender, e) => Console.WriteLine(e.Exception.Message);

            // Start the server (by default it listens on port 53)
           server.Listen();
        }


        static void Main(string[] args)
        {
            Console.WriteLine(string.Join<IPAddress>(" ",Dns.GetHostAddresses(Dns.GetHostName())));


            SendUdp();
            
        }
    }
}
