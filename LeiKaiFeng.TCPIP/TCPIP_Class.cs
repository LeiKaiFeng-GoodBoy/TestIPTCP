using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LeiKaiFeng.TCPIP
{

    public sealed class TCPStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.ReadAsync(buffer, offset, count).Result;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.WriteAsync(buffer, offset, count).Wait();
        }


        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }


    public sealed class TCPLayerInfo
    {
        internal Dictionary<Quaternion, TCPStream> Dic { get; }

        internal IPLayer IPLayer { get; }
        

    }

    public sealed class TCPLayer
    {
        readonly object m_lock = new object();

        readonly TCPLayerInfo m_info;

        private TCPLayer(TCPLayerInfo info)
        {
            m_info = info ?? throw new ArgumentNullException(nameof(info));
        }


        //public static TCPLayer Init(TCPLayerInfo info)
        //{

        //}


        //void Read()
        //{
        //    UPPacket packet = m_info.IPLayer.TakeUPPacket();


        //    TCPData tcpData = packet.ReadTCPData();

            





        //}
    }


    [StructLayout(LayoutKind.Auto)]
    public readonly struct IPData
    {
        public IPData(IPv4Address sourceAddress, IPv4Address desAddress, Protocol protocol)
        {
            SourceAddress = sourceAddress;
            DesAddress = desAddress;
            Protocol = protocol;
        }

        public IPv4Address SourceAddress { get; }

        public IPv4Address DesAddress { get; }

        public Protocol Protocol { get; }
    }


    [StructLayout(LayoutKind.Auto)]
    public readonly struct TCPData
    {
        public ushort SourcePort { get; }

        public ushort DesPort { get; }

        public TCPFlag TCPFlag { get; }

        public ushort WindowSize { get; }

        public uint SequenceNumber { get; }

        public uint AcknowledgmentNumber { get; }




        public TCPData(ref TCPHeader header)
        {

            SourcePort = header.SourcePort;

            DesPort = header.DesPort;

            TCPFlag = header.TCPFlag;

            WindowSize = header.WindowSize;

            SequenceNumber = header.SequenceNumber;

            AcknowledgmentNumber = header.AcknowledgmentNumber;

        }

        public TCPData(ushort sourcePort, ushort desPort, TCPFlag tCPFlag, ushort windowSize, uint sequenceNumber, uint acknowledgmentNumber)
        {
            SourcePort = sourcePort;
            DesPort = desPort;
            TCPFlag = tCPFlag;
            WindowSize = windowSize;
            SequenceNumber = sequenceNumber;
            AcknowledgmentNumber = acknowledgmentNumber;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public sealed class UPPacket
    {
        PacketPool<UPPacket> PacketPool { get; }

        byte[] Array { get; }

        int Offset { get; set; }
        
        int Count { get; set; }

        public Span<byte> Data => Array.AsSpan(Offset, Count);

        public IPData IPData { get; private set; }

        public TCPData TCPData { get; private set; }

        internal bool ReadIPPackeet(Func<byte[], int, int, int> func)
        {
            int count = func(Array, 0, Array.Length);

            Offset = 0;

            Count = count;

            return Read();
        }

        bool Read()
        {
            ref IPHeader header = ref Meth.AsStruct<IPHeader>(Array.AsSpan(Offset));

            if (header.HeadLength != 5)
            {
                return false;
            }
            else
            {
                IPData = new IPData(
                    header.SourceAddress,
                    header.DesAddress,
                    header.Protocol);

                Offset += IPHeader.HEADER_SIZE;

                Count -= IPHeader.HEADER_SIZE;

                if (header.Protocol == Protocol.TCP)
                {
                    ReadTCPData();


                    return true;
                }
                else
                {
                    return false;
                }

            }
        }


        void ReadTCPData()
        {
            ref TCPHeader header = ref Meth.AsStruct<TCPHeader>(Array.AsSpan(Offset));

            int headerSize = header.AllHeaderSize;

            Offset += headerSize;

            Count -= headerSize;

            TCPData = new TCPData(ref header);
        }

       
        internal UPPacket(int size, PacketPool<UPPacket> packetPool)
        {
            PacketPool = packetPool;

            Array = new byte[size];

            Offset = 0;

            Count = 0;
        }

        public void Recycle()
        {
            PacketPool.Set(this);
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public sealed class DownPacket
    {
        PacketPool<DownPacket> PacketPool { get; }


        byte[] Array { get; }

        int Offset { get; set; }


        //Count代表的是是写入的有效字节的长度
        //而不是缓冲区可以使用的长度
        int Count { get; set; }


        int Copy(Span<byte> buffer)
        {
            Span<byte> arrayBuffer = Array.AsSpan(Offset);

            int n;
            if (buffer.Length > arrayBuffer.Length)
            {
                buffer.Slice(0, arrayBuffer.Length).CopyTo(arrayBuffer);

                n = arrayBuffer.Length;
            }
            else
            {
                buffer.CopyTo(arrayBuffer);

                n = buffer.Length;

            }

            Count += n;

            return n;
        }

        public int WriteTCP(
            IPv4Address sourceAddress,
            ushort sourcePort,
            IPv4Address desAddress,
            ushort desPort,
            TCPFlag tcpFlag,
            ushort windowSize,
            uint sequenceNumber,
            uint acknowledgmentNumber,
            Span<byte> buffer)
        {
            
            int count = Copy(buffer);

            Offset -= TCPHeader.HEADER_SIZE;

            Count += TCPHeader.HEADER_SIZE;

            ref TCPHeader header = ref Meth.AsStruct<TCPHeader>(Array.AsSpan(Offset));

            TCPHeader.Set(
                ref header,
                sourceAddress,
                sourcePort,
                desAddress,
                desPort,
                tcpFlag,
                windowSize,
                sequenceNumber,
                acknowledgmentNumber,
                Array.AsSpan(Offset, Count));

            WriteIPHeader(new IPData(sourceAddress, desAddress, Protocol.TCP));

            return count;
        }

        internal void WriteIPPacket(Action<byte[], int, int> action)
        {
            action(Array, Offset, Count);
        }


        void WriteIPHeader(IPData data)
        {
            int count = Count;

            Offset -= IPHeader.HEADER_SIZE;

            Count += IPHeader.HEADER_SIZE;

            ref IPHeader header = ref Meth.AsStruct<IPHeader>(Array.AsSpan(Offset));

            //之前有限制所以转换会不溢出
            IPHeader.Set(ref header, data, (ushort)count);
        }

        internal DownPacket(int size, PacketPool<DownPacket> packetPool)
        {
            PacketPool = packetPool;

            Array = new byte[size];

            InitOffsetCount();
        }


        void InitOffsetCount()
        {
            //给标头预留空间免得复制缓冲区
            Offset = 200;

            Count = 0;
        }

        public void Recycle()
        {
            InitOffsetCount();

            PacketPool.Set(this);
        }
    }

    sealed class PacketPool<T>
    {


        readonly ConcurrentQueue<T> m_queue = new ConcurrentQueue<T>();


        readonly Func<PacketPool<T>, T> m_create;

        public PacketPool(Func<PacketPool<T>, T> create)
        {
            m_create = create ?? throw new ArgumentNullException(nameof(create));
        }

        public T Get()
        {
            if (m_queue.TryDequeue(out T packet))
            {
                return packet;
            }
            else
            {
                return m_create(this);
            }
        }


        public void Set(T packet)
        {
            m_queue.Enqueue(packet);
        }
    }


    public sealed class IPLayerInfo
    {
        public IPLayerInfo(Func<byte[], int, int, int> read, Action<byte[], int, int> write)
        {
            int mtuSize = 65535;

            if (mtuSize < 576 || mtuSize > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(mtuSize), "mtu太小或太大了，偏移会出错");
            }

            
            Read = read ?? throw new ArgumentNullException(nameof(read));
           

            Write = write ?? throw new ArgumentNullException(nameof(write));

            UPPacketPool = new PacketPool<UPPacket>((pool) => new UPPacket(mtuSize, pool));

            DownPacketPool = new PacketPool<DownPacket>((pool) => new DownPacket(mtuSize, pool));

            UPPacketColl = new BlockingCollection<UPPacket>(6);

            DownPacketColl = new BlockingCollection<DownPacket>(6);
        }

        internal Func<byte[], int, int, int> Read { get; }

        internal Action<byte[], int, int> Write { get; }

        internal PacketPool<UPPacket> UPPacketPool { get; }

        internal BlockingCollection<UPPacket> UPPacketColl { get; }


        internal PacketPool<DownPacket> DownPacketPool { get; }

        internal BlockingCollection<DownPacket> DownPacketColl { get; }

    }

    public sealed class IPLayer
    {
        readonly IPLayerInfo m_info;

        private IPLayer(IPLayerInfo info)
        {
            m_info = info ?? throw new ArgumentNullException(nameof(info));
        }


        public static IPLayer Init(IPLayerInfo info)
        {
            return Init(info, (e) => { });
        }


        public static IPLayer Init(IPLayerInfo info, Action<Exception> logAction)
        {
            IPLayer ipLayer = new IPLayer(info);

            CreateThreadAndRun(ipLayer.Read, logAction);

            CreateThreadAndRun(ipLayer.Write, logAction);

            return ipLayer;
        }

        static void CreateThreadAndRun(Action action, Action<Exception> logAction)
        {
            new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    logAction(e);
                }
            }).Start();
        }

        void Write()
        {
            while (true)
            {
                DownPacket packet = m_info.DownPacketColl.Take();

                packet.WriteIPPacket(m_info.Write);

                packet.Recycle();
            }
        }


        void Read()
        {
            while (true)
            {
                UPPacket packet = m_info.UPPacketPool.Get();

                if (packet.ReadIPPackeet(m_info.Read))
                {
                    m_info.UPPacketColl.Add(packet);
                }
                else
                {
                    packet.Recycle();
                   
                    Console.WriteLine("已丢弃ip包");
                }
            }
        }

        public DownPacket CreateDownPacket()
        {
            return m_info.DownPacketPool.Get();
        }


        public UPPacket TakeUPPacket()
        {
            return m_info.UPPacketColl.Take();
        }


        public void AddDownPacket(DownPacket packet)
        {
            m_info.DownPacketColl.Add(packet);
        }
    }



    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct UDPReadData
    {
        public UDPReadData(byte[] buffer, IPv4Address sourceAddress, ushort sourcePort, IPv4Address desAddress, ushort desPort, Span<byte> span, ushort length)
        {
            Buffer = buffer;
            SourceAddress = sourceAddress;
            SourcePort = sourcePort;
            DesAddress = desAddress;
            DesPort = desPort;
            Span = span;

            Length = length;
        }

        public byte[] Buffer { get; }

        public IPv4Address SourceAddress { get; }


        public ushort SourcePort { get; }


        public IPv4Address DesAddress { get; }

        public ushort DesPort { get; }

        public Span<byte> Span { get; }

        public ushort Length { get; }
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct IPReadData
    {
        public IPReadData(byte[] buffer, IPv4Address source, IPv4Address des, Protocol pro, Span<byte> span)
        {
            Buffer = buffer;
            Source = source;
            Des = des;
            Pro = pro;
            Span = span;
        }

        public byte[] Buffer { get; }

        public IPv4Address Source { get; }

        public IPv4Address Des { get; }

        public Protocol Pro { get; }

        public Span<byte> Span { get; }
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct UDPWriteData
    {
        public UDPWriteData(IPv4Address source, ushort sourcePort, IPv4Address des, ushort desPort, ushort length)
        {
            Source = source;
            SourcePort = sourcePort;
            Des = des;
            DesPort = desPort;
            Length = length;
        }

        public IPv4Address Source { get; }

        public ushort SourcePort { get; }


        public IPv4Address Des { get; }


        public ushort DesPort { get; }


        public ushort Length { get; }
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct IPWriteData
    {
        public IPWriteData(IPv4Address source, IPv4Address des, ushort length, Protocol pro)
        {
            Source = source;
            Des = des;
            Length = length;
            Pro = pro;
        }

        public IPv4Address Source { get; }

        public IPv4Address Des { get; }

        public ushort Length { get; }

        public Protocol Pro { get; }
    }


    public sealed class IPProtocol
    {
        readonly IIPUpProtocol m_upProtocol;

        public IPProtocol(IIPUpProtocol upProtocol)
        {
            m_upProtocol = upProtocol;
        }

        public ushort ReadPacket(Span<byte> buffer)
        {
            var data = m_upProtocol.WriteIPPacket(IPHeader.SubHeaderSizeSlice(buffer));

            ref IPHeader header = ref Meth.AsStruct<IPHeader>(buffer);

            return IPHeader.Set(ref header, data);
        }

        public void WritePacket(byte[] buffer)
        {
            Span<byte> span = buffer;

            ref IPHeader header = ref Meth.AsStruct<IPHeader>(span);

            int headerLegnth = header.HeadLength;

            if (headerLegnth != 5)
            {

                Console.WriteLine($"已丢弃ip包");

                return;
            }

            m_upProtocol.ReadIPPacket(new IPReadData(
                   buffer,
                   header.SourceAddress,
                   header.DesAddress,
                   header.Protocol,
                   IPHeader.SubHeaderSizeSlice(span)));
        }


        static void Print(IPHeader header)
        {

            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"Version {header.Version}")
              .AppendLine($"HeaderLength {header.HeadLength}")
              .AppendLine($"AllLength {header.AllLength}")
              .AppendLine($"TTL {header.TTL}")
              .AppendLine($"Protocol {header.Protocol}");



            Console.WriteLine(sb.ToString());


        }
    }

    public sealed class UDPProtocol : IIPUpProtocol
    {
        public void ReadIPPacket(IPReadData readData)
        {
            if (readData.Pro != Protocol.UDP)
            {
                Console.WriteLine("已丢弃udp");

                return;
            }

            ref UDPHeader header = ref Meth.AsStruct<UDPHeader>(readData.Span);

            ReadUDPPacket(
                new UDPReadData(
                    readData.Buffer,
                    readData.Source,
                    header.SourcePort,
                    readData.Des,
                    header.DesPort,
                    UDPHeader.SubHeaderSizeSlice(readData.Span),
                    header.GetSubHeaderSizeLength()));        
        }

        void ReadUDPPacket(UDPReadData readData)
        {

            int offset;

            Meth.GetOffsetCount(
                readData.Buffer,
                readData.Span,
                out offset);


            Console.WriteLine($"{readData.SourceAddress}:{readData.SourcePort} {readData.DesAddress}:{readData.DesPort} {Encoding.UTF8.GetString(readData.Buffer, offset, readData.Length)}");

            Console.WriteLine(Encoding.UTF8.GetString(readData.Buffer, offset, readData.Length));

           
        }

        public IPWriteData WriteIPPacket(Span<byte> buffer)
        {

            var data = WriteUDPPacket(UDPHeader.SubHeaderSizeSlice(buffer));


            ref UDPHeader header = ref Meth.AsStruct<UDPHeader>(buffer);


            ushort length = UDPHeader.Set(ref header, data, buffer);


            return new IPWriteData(data.Source, data.Des, length, Protocol.UDP);
        }     
    

        UDPWriteData WriteUDPPacket(Span<byte> buffer)
        {
           
            return new UDPWriteData(
                new IPv4Address(192, 168, 1, 106),
                5050,
                new IPv4Address(192, 168, 2,2),
                5050,
                (ushort)buffer.Length);
        }
    }
}