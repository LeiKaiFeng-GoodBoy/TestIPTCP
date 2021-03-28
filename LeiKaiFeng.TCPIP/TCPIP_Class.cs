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
            Task task = m_info.BufferWindow.Write(buffer, offset, count);

            Send();

            return task;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return m_info.BufferLoop.Read(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }

    sealed class TCPStreamInfo
    {
        public TCPStreamInfo(TCPLayer tcpLauer, Quaternion quaternion, ushort myWindowSize, ushort desWindowSize, uint acknowledgmentNumber, uint sequenceNumber)
        {
            TCPLayer = tcpLauer;

            BufferLoop = new BufferLoop();

            BufferWindow = new BufferWindow();

            Quaternion = quaternion;
            
            MyWindowSize = myWindowSize;
            
            DesWindowSize = desWindowSize;
            
            AcknowledgmentNumber = acknowledgmentNumber;
            
            SequenceNumber = sequenceNumber;

            OlderSequenceNumber = sequenceNumber;

            NowSequenceNumber = sequenceNumber;

            SeqCount = 0;

            UsedSeqCount = 0;
        }

        internal TCPLayer TCPLayer { get; }

        internal BufferLoop BufferLoop { get; }

        internal BufferWindow BufferWindow { get; }

        internal Quaternion Quaternion { get; }

        internal ushort MyWindowSize { get; set; }

        internal ushort DesWindowSize { get; set; }

        internal uint AcknowledgmentNumber { get; set; }

        internal uint OlderSequenceNumber { get; set; }

        internal uint SequenceNumber { get; set; }

        internal uint NowSequenceNumber { get; set; }

        internal ushort SeqCount { get; set; }

        internal ushort UsedSeqCount { get; set; }
    }


    public sealed partial class TCPStream : Stream
    {
        private readonly object m_lock = new object();

        readonly TCPStreamInfo m_info;

        public Quaternion Quaternion => m_info.Quaternion;

        internal TCPStream(TCPStreamInfo info)
        {

            m_info = info;
        }

        void GetNow()
        {
            const ulong N = (ulong)uint.MaxValue + 1;

            ulong n1 = N + m_info.NowSequenceNumber;

            ulong n2 = m_info.SequenceNumber;

            
        }

        uint GetAck()
        {
            const ulong N = (ulong)uint.MaxValue + 1;

            ulong n1 = N + m_info.SequenceNumber;

            ulong n2 = m_info.OlderSequenceNumber;


            uint n = (uint)(n1 - n2);


            m_info.OlderSequenceNumber = m_info.SequenceNumber;

            return n;
        }


        void DownPacket(DownPacket downPacket)
        {

            lock (m_lock)
            {
                if (m_info.UsedSeqCount != m_info.SeqCount)
                {
                    m_info.UsedSeqCount = m_info.SeqCount;

                    m_info.BufferWindow.SetAck((int)GetAck());
                }


                int n = downPacket.Write(m_info.BufferWindow.Read);
                
                downPacket.WriteTCP(
                    m_info.Quaternion,
                    TCPFlag.ACK,
                    m_info.MyWindowSize,
                    m_info.NowSequenceNumber,
                    m_info.AcknowledgmentNumber,
                    default);

                m_info.NowSequenceNumber += (uint)n;
            }


        }

        void Send()
        {
            m_info.TCPLayer.AddDownPacket(DownPacket);
        }

        internal void WritePacket(UPPacket packet)
        {

            lock (m_lock)
            {
                m_info.SeqCount++;

                m_info.DesWindowSize = packet.TCPData.WindowSize;

                m_info.SequenceNumber = packet.TCPData.AcknowledgmentNumber;

                if (packet.TCPData.SequenceNumber == m_info.AcknowledgmentNumber)
                {

                    int n = m_info.BufferLoop.Write(packet.Array, packet.Offset, packet.Count);

                    m_info.AcknowledgmentNumber += (uint)n;

                    m_info.MyWindowSize = (ushort)m_info.BufferLoop.CanWriteCount;

                    Send();
                }
                else
                {
                    Send();

                    Console.WriteLine("TCP乱序包到达");
                }
            }

            
        }
    }

    sealed partial class BufferWindow : BufferAbstract
    {

        sealed class Item
        {
            public Item(byte[] buffer, int offset, int count)
            {
                TaskCompletionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

                Buffer = buffer;
                Offset = offset;
                Count = count;
            }



            public TaskCompletionSource<object> TaskCompletionSource { get; }

            public Task Task => TaskCompletionSource.Task;

            public byte[] Buffer { get; }

            public int Offset { get; set; }

            public int Count { get; set; }
        }

        private readonly object m_lock = new object();

        readonly Queue<Item> m_queue = new Queue<Item>();


        int m_offset = 0;


        void ReadAdd(int n)
        {
            m_readOffset += n;

            m_readCount -= n;
        }


        void WriteAdd(int n)
        {
            m_writeOffset += n;

            m_writeCount -= n;

            m_readCount += n;
        }


        public int Read(byte[] buffer, int offset, int count)
        {
            lock (m_lock)
            {
                int readOffset = m_readOffset;

                int readCount = m_readCount;

                m_readOffset += m_offset;

                m_readCount -= m_offset;

                int n = Read(ReadAdd, buffer, offset, count);

                m_offset += n;

                m_readOffset = readOffset;

                m_readCount = readCount;

                return n;
            }

            
        }

        public void SetAck(int n)
        {
            
            

            lock (m_lock)
            {
                if (n < 0 || n > m_readCount)
                {
                    Console.WriteLine($"                {n} {m_readCount}");

                    throw new ArgumentOutOfRangeException(nameof(n));
                }

                m_offset = 0;

                m_readOffset += n;

                m_readCount -= n;

                m_writeCount += n;

                Write();
            }
        }



        void Write()
        {
            //这里Count不能为0

            while (m_queue.Count != 0)
            {

                var item = m_queue.Peek();



                int n = Write(WriteAdd, item.Buffer, item.Offset, item.Count);

                if (n == 0)
                {
                    return;
                }
                else
                {
                    item.Offset += n;

                    item.Count -= n;

                    if (item.Count == 0)
                    {

                        m_queue.Dequeue();

                        item.TaskCompletionSource.TrySetResult(default);
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }


        public Task Write(byte[] buffer, int offset, int count)
        {
            if (count == 0)
            {
                return Task.CompletedTask;
            }

            lock (m_lock)
            {
                var item = new Item(buffer, offset, count);

                m_queue.Enqueue(item);

                var task = item.Task;

                Write();

                return task;
            }
        }
    }

    sealed class BufferLoop : BufferAbstract
    {

        readonly struct Item
        {
            public Item(byte[] buffer, int offset, int count)
            {
                TaskCompletionSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

                Buffer = buffer;
                Offset = offset;
                Count = count;
            }

            public TaskCompletionSource<int> TaskCompletionSource { get; }


            public Task<int> Task => TaskCompletionSource.Task;

            public byte[] Buffer { get; }

            public int Offset { get; }

            public int Count { get; }


        }


        private readonly object m_lock = new object();

        readonly Queue<Item> m_queue = new Queue<Item>();

        public int Write(byte[] buffer, int offset, int count)
        {
            lock (m_lock)
            {
                int n = Write(WriteAdd, buffer, offset, count);

                if (n != count)
                {
                    Read();

                    n += Write(WriteAdd, buffer, offset + n, count - n);

                    Read();
                }
                else
                {
                    Read();
                }

                return n;
            }
        }


        void Read()
        {
            while (m_queue.Count != 0)
            {
                var item = m_queue.Peek();

                int n = Read(ReadAdd, item.Buffer, item.Offset, item.Count);

                if (n == 0)
                {
                    return;
                }
                else
                {
                    m_queue.Dequeue();

                    item.TaskCompletionSource.TrySetResult(n);
                }
            }
        }


        public Task<int> Read(byte[] buffer, int offset, int count)
        {
            lock (m_lock)
            {
                var item = new Item(buffer, offset, count);

                m_queue.Enqueue(item);

                var task = item.Task;

                Read();

                return task;
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


    }


    abstract class BufferAbstract
    {
        readonly byte[] m_buffer;

        protected int m_readOffset;

        protected int m_readCount;

        protected int m_writeOffset;

        protected int m_writeCount;

        public int CanReadCount => m_readCount;

        public int CanWriteCount => m_writeCount;

        protected BufferAbstract()
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

        

        protected int Write(Action<int> add, byte[] buffer, int offset, int count)
        {
            return Copy(m_buffer, ref m_writeOffset, ref m_writeCount,
                buffer, offset, count,
                add,
                (source, sourceOffset, sourceCount,
                desbuffer, desoffset, descount) =>
                CopyTo(desbuffer, desoffset, descount,
                    source, sourceOffset, sourceCount));
        }

        protected int Read(Action<int> add, byte[] buffer, int offset, int count)
        {
            return Copy(m_buffer, ref m_readOffset, ref m_readCount,
                buffer, offset, count,
                add,
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

   

    

    sealed class HandshakePhase
    {
        public HandshakePhase(uint sequenceNumber, uint acknowledgmentNumber, Quaternion quaternion)
        {
            SequenceNumber = sequenceNumber;
            AcknowledgmentNumber = acknowledgmentNumber;
            Quaternion = quaternion;
        }

        public uint SequenceNumber { get; set; }

        public uint AcknowledgmentNumber { get; set; }


        public Quaternion Quaternion { get; }

        public void DownPacket(DownPacket downPacket)
        {
            downPacket.WriteTCP(
                    Quaternion,
                    TCPFlag.ACK | TCPFlag.SYN,
                    ushort.MaxValue,
                    SequenceNumber,
                    AcknowledgmentNumber,
                    default);

        }
    }


    //一开始C发送一个起始序号CX，确认序号无意义
    //我发一个确认序号CX+1， 发一个起始序号SX
    //然后C发送一个确认序号SX+1，发送一个起始序号CX+1
    //也就是说对面发送的是我确认的


    public sealed class TCPLayerInfo
    {

        

        public TCPLayerInfo(Action<TCPStream> intoConnect)
        {
            
            IntoConnect = intoConnect ?? throw new ArgumentNullException(nameof(intoConnect));

            Dic = new Dictionary<Quaternion, TCPStream>();

            HPDic = new Dictionary<Quaternion, HandshakePhase>();

            DownPacketColl = new BlockingCollection<Action<DownPacket>>();
        }

        internal Dictionary<Quaternion, TCPStream> Dic { get; }

        internal Dictionary<Quaternion, HandshakePhase> HPDic { get; }

        internal Action<TCPStream> IntoConnect { get; }

        internal BlockingCollection<Action<DownPacket>> DownPacketColl { get; }
    }


    public sealed class TCPLayer
    {
        readonly TCPLayerInfo m_info;

        private TCPLayer(TCPLayerInfo info)
        {
            m_info = info ?? throw new ArgumentNullException(nameof(info));
        }


        public static TCPLayer Init(TCPLayerInfo info)
        {
            TCPLayer layer = new TCPLayer(info);

            return layer;
        }

        void InitHandshake(UPPacket upPacket)
        {
            if (m_info.HPDic.ContainsKey(upPacket.Quaternion))
            {
                Console.WriteLine("已经存在了一个进行中的握手");
            }
            else
            {
                //我的开头编号用别人的，我就不用自己管理了
                uint seq = upPacket.TCPData.SequenceNumber;
                uint ackSeq = upPacket.TCPData.SequenceNumber + 1;

                HandshakePhase handshakePhase = new HandshakePhase(
                    seq,
                    ackSeq,
                    upPacket.Quaternion);

                m_info.HPDic.Add(upPacket.Quaternion, handshakePhase);

                AddDownPacket(handshakePhase.DownPacket);

                Console.WriteLine("一个握手开始");
            }
        }

        void OKHandshanke(UPPacket packet, HandshakePhase handshake)
        {
            if (m_info.Dic.ContainsKey(packet.Quaternion))
            {
                Console.WriteLine("握手成功一个，但已经存在了一个TCP");
            }
            else
            {
                Console.WriteLine("握手成功一个");

                TCPStream stream = new TCPStream(new TCPStreamInfo(this, packet.Quaternion, ushort.MaxValue, packet.TCPData.WindowSize, handshake.AcknowledgmentNumber, handshake.SequenceNumber));

                m_info.Dic.Add(packet.Quaternion, stream);

                Task.Run(() => m_info.IntoConnect(stream));
            }
        }

        void FACK(UPPacket packet)
        {
            if (m_info.HPDic.ContainsKey(packet.Quaternion))
            {
                HandshakePhase handshake = m_info.HPDic[packet.Quaternion];

                m_info.HPDic.Remove(packet.Quaternion);

                if (handshake.SequenceNumber + 1 == packet.TCPData.AcknowledgmentNumber &&
                    handshake.AcknowledgmentNumber == packet.TCPData.SequenceNumber)
                {

                    handshake.SequenceNumber = packet.TCPData.AcknowledgmentNumber;

                    handshake.AcknowledgmentNumber = packet.TCPData.SequenceNumber;

                }
                else
                {
                    Console.WriteLine("存在一个序号不匹配的握手");

                    return;
                }

                OKHandshanke(packet, handshake);
            }
            else if (m_info.Dic.ContainsKey(packet.Quaternion))
            {
                TCPStream stream = m_info.Dic[packet.Quaternion];

                stream.WritePacket(packet);
            }
            else
            {
                Console.WriteLine("一个既不是握手也不是链接的包");
            }
        }


        internal void AddDownPacket(Action<DownPacket> action)
        {
            m_info.DownPacketColl.Add(action);
        }

        public void DownPacket(DownPacket downPacket)
        {
            var action = m_info.DownPacketColl.Take();

            action(downPacket);
        }

        public void UPPacket(UPPacket packet)
        {

            
            TCPFlag flag = packet.TCPData.TCPFlag;

            if (flag == TCPFlag.SYN)
            {
                InitHandshake(packet);
            }
            else if (Meth.HasFlag(flag, TCPFlag.RST | TCPFlag.FIN))
            {

            }
            else if (Meth.HasFlag(flag, TCPFlag.ACK))
            {
                FACK(packet);
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
        
        public byte[] Array { get; }

        public int Offset { get; private set; }
        
        public int Count { get; private set; }

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

       
        internal UPPacket(int size)
        {
           
            Array = new byte[size];

            Offset = 0;

            Count = 0;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public sealed class DownPacket
    {
       
        byte[] Array { get; }

        int Offset { get; set; }


        //Count代表的是是写入的有效字节的长度
        //而不是缓冲区可以使用的长度
        int Count { get; set; }

        public int Write(Func<byte[], int, int, int> func)
        {
            int n = func(Array, Offset, Array.Length - Offset);

            Count += n;

            return n;

        }

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

        internal DownPacket(int size)
        {
           
            Array = new byte[size];

            InitOffsetCount();
        }


        internal void InitOffsetCount()
        {
            //给标头预留空间免得复制缓冲区
            Offset = 200;

            Count = 0;
        }

    }






    public sealed class IPLayerInfo
    {
        public IPLayerInfo(Func<byte[], int, int, int> read, Action<byte[], int, int> write, Action<UPPacket> upPacket, Action<DownPacket> downPacket)
        {
            int mtuSize = 65535;

            if (mtuSize < 576 || mtuSize > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(mtuSize), "mtu太小或太大了，偏移会出错");
            }



            Read = read ?? throw new ArgumentNullException(nameof(read));
            Write = write ?? throw new ArgumentNullException(nameof(write));
            UPPacket = upPacket ?? throw new ArgumentNullException(nameof(upPacket));
            DownPacket = downPacket ?? throw new ArgumentNullException(nameof(downPacket));
        }

        internal Func<byte[], int, int, int> Read { get; }

        internal Action<byte[], int, int> Write { get; }

        internal Action<UPPacket> UPPacket { get; }

        internal Action<DownPacket> DownPacket { get; }
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
            DownPacket downPacket = new DownPacket(1024);
            while (true)
            {
                Console.WriteLine("Write");
                downPacket.InitOffsetCount();

                m_info.DownPacket(downPacket);

                downPacket.WriteIPPacket(m_info.Write);
            }
        }


        void Read()
        {
            UPPacket upPacket = new UPPacket(ushort.MaxValue);
            while (true)
            {
                
                if (upPacket.ReadIPPackeet(m_info.Read))
                {
                    m_info.UPPacket(upPacket);
                }
                else
                {
                    
                    Console.WriteLine("已丢弃ip包");
                }
            }
        }
    }
}