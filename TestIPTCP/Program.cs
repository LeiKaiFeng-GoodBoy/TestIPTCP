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
using DNS.Server;
using DNS.Protocol;
using System.Threading.Tasks;
using DNS.Protocol.ResourceRecords;
using DNS.Client.RequestResolver;
using System.Collections.Generic;

namespace TestIPTCP
{

    public sealed class LocalRequestResolver : IRequestResolver
    {
        

        MasterFile File { get; }

        public LocalRequestResolver(MasterFile masterFile)
        {
            File = masterFile ?? throw new ArgumentNullException(nameof(masterFile));
        }

        public Task<IResponse> Resolve(IRequest request, CancellationToken cancellationToken = default)
        {

            IResponse response = File.Resolve(request, cancellationToken).Result;

            if (response.Questions.Count == 1)
            {
                Question question = response.Questions.First();

                if (question.Type == RecordType.A || question.Type == RecordType.AAAA) 
                {
                    var resourceRecord =
                        (IPAddressResourceRecord)response
                        .AnswerRecords
                        .FirstOrDefault();

                    if (resourceRecord is null)
                    {
                        Console.WriteLine("null");
                    }
                    else
                    {
                        response.AnswerRecords.Remove(resourceRecord);

                        response
                            .AnswerRecords
                            .Add(new IPAddressResourceRecord(
                                question.Name,
                                resourceRecord.IPAddress));
                    }
                }
            }
            

            return Task.FromResult(response);
        }


        public static Socket CreateDNSServer(
            MasterFile masterFile,
            IPAddress defaultDnsServer,
            IPEndPoint localDnsServerBind,
            IPEndPoint localUDPBind)
        {
            //为什么要包装一下MasterFile
            //主要是因为有的浏览器假如应答的域名是通配符，他会认为DNS无效
            //所以把通配符改为请求中的绝对域名
            //也就是说MasterFile会返回通配符

            DnsServer server = new DnsServer(new LocalRequestResolver(masterFile), defaultDnsServer);

            server.Listen(localDnsServerBind);


            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            socket.Bind(localUDPBind);

            socket.Connect(localDnsServerBind);

            return socket;
        }

    }



    class Program
    {


        

        static Socket DNS()
        {
           
            MasterFile masterFile = new MasterFile();
           
            masterFile.AddIPAddressResourceRecord("*.iwara.tv", "141.101.120.83");
            masterFile.AddIPAddressResourceRecord("t66y.com", "141.101.120.83");
            masterFile.AddIPAddressResourceRecord("ajax.googleapis.com", "127.0.0.5");


            Socket socket = LocalRequestResolver.CreateDNSServer(masterFile,
                IPAddress.Parse("114.114.114.114"),
                new IPEndPoint(IPAddress.Loopback, 54663),
                new IPEndPoint(IPAddress.Loopback, 36645));

            return socket;

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


        static void Start(Func<byte[], int, int, int> read, Action<byte[], int, int> write, Socket socket)
        {
            var readPacket = new ReadUDPPacket(ushort.MaxValue);

            var writePacket = new WriteUDPPacket(ushort.MaxValue);

            //这个地方主要是为了简单，否则要保存链接之间的映射关系，还要必要使拆除
            //这样读一个请求，返回一个应答，不需要保存映射关系

            while (true)
            {
                writePacket.InitOffsetCount();

                if (readPacket.Read(read))
                {

                    socket.Send(readPacket.Array, readPacket.Offset, readPacket.Count, SocketFlags.None);


                    writePacket.WriteUDP((buffer, offset, count) => socket.Receive(buffer, offset, count, SocketFlags.None));

                    writePacket.WriteUDP(readPacket.Quaternion.Reverse());


                    writePacket.Write(write);
                }
                else
                {
                    Console.WriteLine("error");
                }
            }
        }

        static void Udp()
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            socket.Bind(new IPEndPoint(IPAddress.Parse("192.168.1.106"), 5050));

            socket.Connect(new IPEndPoint(IPAddress.Parse("192.168.1.104"), 5050));

            //\\.\pipe\MMYY
            var wir = WiresharkSender.Create("MMYY");

            var read = ReadUDPAndWir(socket, wir);

            //Func<byte[], int,int,int> read = (buffer, offset, count) => socket.Receive(buffer, offset, count, SocketFlags.None);

            var write = WriteUDPAndWir(socket, wir);


            //Action<byte[], int, int> write = (buffer, offset, count) => socket.Send(buffer, offset, count, SocketFlags.None);

            var con = DNS();


            Start(read, write, con);


            Console.ReadLine();
        }

        static void Main()
        {
            
            Udp();
        }
    }
}