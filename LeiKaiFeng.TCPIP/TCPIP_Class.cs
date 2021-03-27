﻿using System;
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

            OlderAcknowledgmentNumber = OlderAcknowledgmentNumber;

            AckCount = 0;

            UsedAckCount = 0;
        }

        internal TCPLayer TCPLayer { get; }

        internal BufferLoop BufferLoop { get; }

        internal BufferWindow BufferWindow { get; }

        internal Quaternion Quaternion { get; }

        internal ushort MyWindowSize { get; set; }

        internal ushort DesWindowSize { get; set; }

        internal uint AcknowledgmentNumber { get; set; }

        internal uint OlderAcknowledgmentNumber { get; set; }

        internal uint SequenceNumber { get; set; }

        internal ushort AckCount { get; set; }

        internal ushort UsedAckCount { get; set; }



    }


    public sealed partial class TCPStream : Stream
    {
        

        readonly TCPStreamInfo m_info;

        public Quaternion Quaternion => m_info.Quaternion;

        internal TCPStream(TCPStreamInfo info)
        {

            m_info = info;
        }

        uint GetAck()
        {
            const ulong N = 0x01 << 32;

            ulong n1 = N + m_info.AcknowledgmentNumber;

            ulong n2 = m_info.OlderAcknowledgmentNumber;

            uint n = (uint)(n1 - n2);


            m_info.OlderAcknowledgmentNumber = m_info.AcknowledgmentNumber;

            return n;
        }


        void DownPacket(DownPacket downPacket)
        {

            if (m_info.UsedAckCount == m_info.AckCount)
            {
                int n = downPacket.Write(m_info.BufferWindow.Read);

                m_info.SequenceNumber += (uint)n;

                downPacket.WriteTCP(
                    m_info.Quaternion,
                    TCPFlag.ACK,
                    m_info.MyWindowSize,
                    m_info.SequenceNumber,
                    m_info.AcknowledgmentNumber,
                    default);
            }
            else
            {
                m_info.UsedAckCount = m_info.AckCount;

                m_info.BufferWindow.SetAck((int)GetAck());

                int n = downPacket.Write(m_info.BufferWindow.Read);

                m_info.SequenceNumber += (uint)n;

                downPacket.WriteTCP(
                    m_info.Quaternion,
                    TCPFlag.ACK,
                    m_info.MyWindowSize,
                    m_info.SequenceNumber,
                    m_info.AcknowledgmentNumber,
                    default);

            }
        }

        void Send()
        {
            m_info.TCPLayer.AddDownPacket(DownPacket);
        }

        internal void WritePacket(UPPacket packet)
        {

            m_info.AckCount++;

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
            if (n < 0 || n > m_readCount)
            {
                throw new ArgumentOutOfRangeException(nameof(n));
            }

            lock (m_lock)
            {

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


        public static TCPLayer Init(TCPLayerInfo info, Action<Exception> logAction)
        {
            TCPLayer layer = new TCPLayer(info);

            return layer;
        }

        void Init(UPPacket upPacket)
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

                m_info.DownPacketColl.Add(handshakePhase.DownPacket);

                Console.WriteLine("一个握手开始");
            }
        }

        void Add(UPPacket packet, HandshakePhase handshake)
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

        void Add(UPPacket packet)
        {
            if (m_info.HPDic.ContainsKey(packet.Quaternion))
            {
                HandshakePhase handshake = m_info.HPDic[packet.Quaternion];

                m_info.HPDic.Remove(packet.Quaternion);

                handshake.SequenceNumber = packet.TCPData.AcknowledgmentNumber;

                handshake.AcknowledgmentNumber = packet.TCPData.SequenceNumber;

                Add(packet, handshake);
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
            m_info.DownPacketColl.TryAdd(action);
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
                Init(packet);
            }
            else if (Meth.HasFlag(flag, TCPFlag.RST | TCPFlag.FIN))
            {

            }
            else if (Meth.HasFlag(flag, TCPFlag.ACK))
            {
                Add(packet);
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
        public IPLayerInfo(Func<byte[], int, int, int> read, Action<byte[], int, int> write, Action<UPPacket> uPPacket, Action<DownPacket> downPacket)
        {
            int mtuSize = 65535;

            if (mtuSize < 576 || mtuSize > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(mtuSize), "mtu太小或太大了，偏移会出错");
            }



            Read = read ?? throw new ArgumentNullException(nameof(read));
            Write = write ?? throw new ArgumentNullException(nameof(write));
            UPPacket = uPPacket ?? throw new ArgumentNullException(nameof(uPPacket));
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
            DownPacket downPacket = new DownPacket(ushort.MaxValue);
            while (true)
            {
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