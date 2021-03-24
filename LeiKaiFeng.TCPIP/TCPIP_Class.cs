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
using System.Threading.Channels;
using System.Threading.Tasks;

namespace LeiKaiFeng.TCPIP
{

    public sealed partial class TCPStream : Stream
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


    sealed class BufferItem
    {
        readonly byte[] m_buffer;

        int m_readOffset;

        int m_readCount;

        int m_writeOffset;

        int m_writeCount;

        public BufferItem()
        {
            m_buffer = new byte[ushort.MaxValue];

            m_readCount = 0;

            m_readOffset = 0;

            m_writeCount = m_buffer.Length;

            m_writeOffset = 0;
        }

        static int CopyTo(byte[] sourceBuffer, int sourceOffset, int sourceCount,
                           byte[] desBuffer, int desOffset, int desCount)
        {
            var source = sourceBuffer.AsSpan(sourceOffset, sourceCount);

            var des = desBuffer.AsSpan(desOffset, desCount);


            if (source.Length > des.Length)
            {
                source.Slice(0, des.Length).CopyTo(des);

                return des.Length;
            }
            else
            {
                source.CopyTo(des);

                return source.Length;
            }
        }

        void ReadAdd(int n)
        {
            m_readOffset += n;

            m_readCount -= n;

            m_writeCount += n;

        }


        void WriteAdd(int n)
        {
            m_writeOffset += n;

            m_writeCount -= n;

            m_readCount += n;
        }

       
        public int Write(byte[] buffer, int offset, int count)
        {
            return Copy(m_buffer, ref m_writeOffset, ref m_writeCount,
                buffer, offset, count,
                WriteAdd,
                (source, sourceOffset, sourceCount,
                desbuffer, desoffset, descount) =>
                CopyTo(desbuffer, desoffset, descount,
                    source, sourceOffset, sourceCount));
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            return Copy(m_buffer, ref m_readOffset, ref m_readCount,
                buffer, offset, count,
                ReadAdd,
                (source, sourceOffset, sourceCount,
                desbuffer, desoffset, descount) =>
                CopyTo(source, sourceOffset, sourceCount,
                desbuffer, desoffset, descount));
        }

        static int Copy(
            byte[] source, ref int sourceOffset, ref int sourceCount,
            byte[] buffer, int offset, int count,
            Action<int> add,
            Func<byte[], int, int, byte[], int, int, int> copy)
        {

            int allCount = sourceOffset + sourceCount;

            
            if (allCount > source.Length)
            {

                int n = copy(source, sourceOffset, source.Length - sourceOffset,
                               buffer, offset, count);


                add(n);

                if (sourceOffset >= source.Length)
                {
                    sourceOffset -= source.Length;

                    offset += n;

                    count -= n;

                    return n + Copy(source, ref sourceOffset, ref sourceCount,
                                 buffer, offset, count,
                                 add,
                                 copy);
                }
                else
                {
                    return n;
                }
            }
            else
            {
                //偏移是0，计数最大
                //偏移最大，计数是0，
                //偏移和计数的和小于总长度
                //因为if的条件所以当前writeOffset不会越界，也无需调整
                int n = copy(source, sourceOffset, sourceCount,
                               buffer, offset, count);


                add(n);

                return n;
            }


        }
    }


    sealed class UPBuffer
    {
        byte[] m_buffer_0;

        byte[] m_buffer_1;


    }

    sealed class TCPStreamInfo
    {
        public TCPStreamInfo(Quaternion quaternion, uint acknowledgmentNumber, uint sequenceNumber)
        {
            var channel = Channel.CreateBounded<UPPacket>(6);

            ChannelReader = channel;

            ChannelWriter = channel;

            Quaternion = quaternion;
            AcknowledgmentNumber = acknowledgmentNumber;
            SequenceNumber = sequenceNumber;
        }

        internal ChannelWriter<UPPacket> ChannelWriter { get; }

        internal ChannelReader<UPPacket> ChannelReader { get; }

        internal Quaternion Quaternion { get; }

        internal uint AcknowledgmentNumber { get; set; }

        internal uint SequenceNumber { get; set; }

    }

    public sealed partial class TCPStream : Stream
    {
        private readonly object m_lock = new object();

        readonly TCPStreamInfo m_info;

        public TCPStream(Quaternion quaternion, uint acknowledgmentNumber, uint sequenceNumber)
        {

            m_info = new TCPStreamInfo(quaternion, acknowledgmentNumber, sequenceNumber);
        }

        
        void SendACK(uint ackSeq, ushort WindwsSize)
        {

        }

        internal Task<int> ReadAsync(byte[] buffer, int offset, int size)
        {
            m_info.ChannelReader.read
        }

       
        internal void WritePacket(UPPacket packet)
        {
            lock (m_lock)
            {
                if (packet.TCPData.SequenceNumber == m_info.AcknowledgmentNumber)
                {
                    uint n = (uint)packet.Data.Length;

                    if (m_info.ChannelWriter.TryWrite(packet))
                    {
                        m_info.AcknowledgmentNumber += n;

                        SendACK(m_info.AcknowledgmentNumber, ushort.MaxValue);
                    }
                    else
                    {
                        SendACK(m_info.AcknowledgmentNumber, 0);
                    }
                }
                else
                {
                    Console.WriteLine("TCP乱序包到达");
                }
            }
        }
    }

    public sealed class TCPLayerInfo
    {
        public TCPLayerInfo(IPLayer iPLayer, Action<TCPStream> intoConnect)
        {
            IPLayer = iPLayer ?? throw new ArgumentNullException(nameof(iPLayer));
            IntoConnect = intoConnect ?? throw new ArgumentNullException(nameof(intoConnect));

            Dic = new Dictionary<Quaternion, TCPStream>();

            HPDic = new Dictionary<Quaternion, HandshakePhase>();
        }

        internal Dictionary<Quaternion, TCPStream> Dic { get; }

        internal Dictionary<Quaternion, HandshakePhase> HPDic { get; }

        internal IPLayer IPLayer { get; }
        
        internal Action<TCPStream> IntoConnect { get; }
    }

    //一开始C发送一个起始序号CX，确认序号无意义
    //我发一个确认序号CX+1， 发一个起始序号SX
    //然后C发送一个确认序号SX+1，发送一个起始序号CX+1
    //也就是说对面发送的是我确认的

    sealed class HandshakePhase
    {
        public HandshakePhase(uint sequenceNumber, uint acknowledgmentNumber)
        {
            SequenceNumber = sequenceNumber;
            AcknowledgmentNumber = acknowledgmentNumber;
        }

        uint SequenceNumber { get; set; }

        uint AcknowledgmentNumber { get; set; }


        public static void Init(TCPLayerInfo info, UPPacket upPacket)
        {      
            if (info.HPDic.ContainsKey(upPacket.Quaternion))
            {
                Console.WriteLine("已经存在了一个进行中的握手");
            }
            else
            {
                //我的开头编号用别人的，我就不用自己管理了
                uint seq = upPacket.TCPData.SequenceNumber;
                uint ackSeq = upPacket.TCPData.SequenceNumber + 1;

                DownPacket downPacket = info.IPLayer.CreateDownPacket();

                downPacket.WriteTCP(
                    upPacket.Quaternion,
                    TCPFlag.ACK | TCPFlag.SYN,
                    ushort.MaxValue,
                    seq,
                    ackSeq,
                    default);

                info.IPLayer.AddDownPacket(downPacket);

               
                HandshakePhase handshakePhase = new HandshakePhase(
                    seq,
                    ackSeq);

                info.HPDic.Add(upPacket.Quaternion, handshakePhase);

                Console.WriteLine("一个握手开始");
            }
        }

        static void Add(TCPLayerInfo info, UPPacket packet, HandshakePhase handshake)
        {
            if (info.Dic.ContainsKey(packet.Quaternion))
            {
                Console.WriteLine("握手成功一个，但已经存在了一个TCP");
            }
            else
            {
                Console.WriteLine("握手成功一个");

                TCPStream stream = new TCPStream(packet.Quaternion, handshake.AcknowledgmentNumber, handshake.SequenceNumber);

                info.Dic.Add(packet.Quaternion, stream);

                Task.Run(() => info.IntoConnect(stream));
            }
        }

        public static void AddPacket(TCPLayerInfo info, UPPacket packet)
        {
            if (info.Dic.ContainsKey(packet.Quaternion))
            {
                TCPStream stream = info.Dic[packet.Quaternion];

                
            }
            else
            {
                Console.WriteLine("一个没有建立连接的包");
            }
        }


        public static void Add(TCPLayerInfo info, UPPacket packet)
        {
            if (info.HPDic.ContainsKey(packet.Quaternion))
            {
                HandshakePhase handshake = info.HPDic[packet.Quaternion];

                info.HPDic.Remove(packet.Quaternion);

                handshake.SequenceNumber = packet.TCPData.AcknowledgmentNumber;

                handshake.AcknowledgmentNumber = packet.TCPData.SequenceNumber;

                Add(info, packet, handshake);
            }
            else
            {
                
            }
        }
    }

    public sealed class TCPLayer
    {
        readonly object m_lock = new object();

        readonly TCPLayerInfo m_info;

        private TCPLayer(TCPLayerInfo info)
        {
            m_info = info ?? throw new ArgumentNullException(nameof(info));
        }


        public static TCPLayer Init(TCPLayerInfo info, Action<Exception> logAction)
        {
            TCPLayer layer = new TCPLayer(info);

            Meth.CreateThreadAndRun(layer.ReadLoop, logAction);

            return layer;
        }

        void ReadLoop()
        {
            while (true)
            {
                Read();
            }
        }


        void Read()
        {

            UPPacket packet = m_info.IPLayer.TakeUPPacket();


            TCPFlag flag = packet.TCPData.TCPFlag;

            if (flag == TCPFlag.SYN)
            {
                HandshakePhase.Init(m_info, packet);
            }
            else if (Meth.HasFlag(flag, TCPFlag.RST | TCPFlag.FIN))
            {

            }
            else if (Meth.HasFlag(flag, TCPFlag.ACK))
            {
                HandshakePhase.Add(m_info, packet);
            }
            else
            {

            }


        }
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

        public Quaternion Quaternion =>
            new Quaternion(
                new IPv4EndPoint(IPData.SourceAddress, TCPData.SourcePort),
                new IPv4EndPoint(IPData.DesAddress, TCPData.DesPort));

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
            Quaternion quaternion,
            TCPFlag tcpFlag,
            ushort windowSize,
            uint sequenceNumber,
            uint acknowledgmentNumber,
            Span<byte> buffer)
        {

            quaternion = quaternion.Reverse();

            int count = Copy(buffer);

            Offset -= TCPHeader.HEADER_SIZE;

            Count += TCPHeader.HEADER_SIZE;

            ref TCPHeader header = ref Meth.AsStruct<TCPHeader>(Array.AsSpan(Offset));

            TCPHeader.Set(
                ref header,
                quaternion.Source.Address,
                quaternion.Source.Port,
                quaternion.Des.Address,
                quaternion.Des.Port,
                tcpFlag,
                windowSize,
                sequenceNumber,
                acknowledgmentNumber,
                Array.AsSpan(Offset, Count));

            WriteIPHeader(new IPData(quaternion.Source.Address, quaternion.Des.Address, Protocol.TCP));

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

            Meth.CreateThreadAndRun(ipLayer.Read, logAction);

            Meth.CreateThreadAndRun(ipLayer.Write, logAction);

            return ipLayer;
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
                    
                    //Console.WriteLine("已丢弃ip包");
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