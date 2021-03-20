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


        static void Main(string[] args)
        {
            SendUdp();
            
        }
    }
}
