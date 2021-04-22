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


        public static void Start(Func<byte[], int, int, int> read, Action<byte[], int, int> write, Socket socket)
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
            byte[] readArray = new byte[ushort.MaxValue];

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
            
            byte[] readArray = new byte[ushort.MaxValue];

            int index = WriteSource(readArray);


            return (buffer, offset, count) =>
            {
                int n = socket.Receive(readArray, index, readArray.Length - index, SocketFlags.None);


                sender.SendToWireshark(readArray, 0, n + index);

                readArray.AsSpan(index, n).CopyTo(buffer.AsSpan(offset, count));

                return n;
            };
        }

        static Func<Quaternion, Quaternion> CreateAs(ushort sourcePort, ushort desPort)
        {
            //一言蔽之，就是，
            //因为TUN读取出来的是IP数据包，而我又写不出TCP协议，索性利用系统的TCP实现
            //利用办法就是，把读取出的IP数据包和TCP数据包的包头的IP和端口修改，再重新写回TUN


            //原始地址是确定的，都是设置的TUN的地址
            //目标端口只支持443或者只支持80，那么目标端口也是确定的
            //目的地址不确定
            //原始端口不确定
            //也就是说我们只需要记录目的地址与原始端口
            //又因为IP数据包的去和回本身也会携带信息，目的端点会变成原始端点返回
            //则将目的地址转换为原始地址
            //将原始端口转换为目的端口
            //这样就能保存目的地址与原始端口
            //我们本身不需要保存任何状态
            //也不需要实现TCP协议
            //就能实现一个虽然不通用，却特定的应用
            //缺点是占用一个TCP端口，并且外界无法主动与内部通信，等等
            //类似于把其他网站全部汇集到一个IP:PORT上，通过HTTPS的SNI或者HTTP Host来判断请求的主机

            return (que) =>
            {

                if (que.Source.Port == desPort)
                {
                    return new Quaternion(
                        source: new IPv4EndPoint(que.Des.Address, sourcePort),
                        des: new IPv4EndPoint(que.Source.Address, que.Des.Port));
                }
                else
                if (que.Des.Port == sourcePort)
                {
                    return new Quaternion(
                       source: new IPv4EndPoint(que.Des.Address, que.Source.Port),
                       des: new IPv4EndPoint(que.Source.Address, desPort));


                }
                else
                {
                    Console.WriteLine("port 错误");

                    return que;
                }
            };
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

            byte[] buffer = new byte[ushort.MaxValue];

            while (true)
            {

                int n = read(buffer, 0, buffer.Length);

                AsTo.As(buffer.AsSpan(0, n), CreateAs(80, 8888));



                write(buffer, 0, n);
            }

            Console.ReadLine();
        }

        static void Main()
        {
            
            Udp();
        }
    }
}