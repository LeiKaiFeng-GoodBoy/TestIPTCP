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
            return Task.CompletedTask;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return m_info.ReadBuffer.Read(buffer, offset, count);
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

            ReadBuffer = new ReadBuffer(sequenceNumber);

            Quaternion = quaternion;
            
            MyWindowSize = myWindowSize;
            
            DesWindowSize = desWindowSize;
            
            AcknowledgmentNumber = acknowledgmentNumber;
            
            SequenceNumber = sequenceNumber;

            OlderSequenceNumber = sequenceNumber;

            NowSequenceNumber = sequenceNumber;

            SeqCount = 0;

            UsedSeqCount = 0;

            IsStartAck = false;
        }

        internal TCPLayer TCPLayer { get; }

        internal ReadBuffer ReadBuffer { get; }

        internal Quaternion Quaternion { get; }

        internal ushort MyWindowSize { get; set; }

        internal ushort DesWindowSize { get; set; }

        internal uint AcknowledgmentNumber { get; set; }

        internal uint OlderSequenceNumber { get; set; }

        internal uint SequenceNumber { get; set; }

        internal uint NowSequenceNumber { get; set; }

        internal ushort SeqCount { get; set; }

        internal ushort UsedSeqCount { get; set; }

        internal bool IsStartAck { get; set; }
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

        
        void ACK()
        {
            DownPacket downPacket = new DownPacket(600);

            downPacket.WriteTCP(
                m_info.Quaternion,
                 TCPFlag.ACK,
                 m_info.MyWindowSize,
                 m_info.AcknowledgmentNumber,
                 m_info.SequenceNumber,
                 default);


            m_info.TCPLayer.Write(downPacket);


        }


        internal void WritePacket(UPPacket packet)
        {

            lock (m_lock)
            {

                uint seq = packet.TCPData.SequenceNumber;

                bool b = m_info.ReadBuffer.Write(ref seq, out var windowSize, packet.Array, packet.Offset, packet.Count);

                m_info.SequenceNumber = seq;

                m_info.MyWindowSize = windowSize;

                if (b)
                {
                    ACK();
                }
                else
                {
                    ACK();
                    ACK();
                    ACK();
                    ACK();
                }
            }

            
        }
    }


    static class BufferMethod
    {
        interface ICopy
        {
            int Copy(Span<byte> source, Span<byte> des);
        }


        public struct ArrayItem
        {

            public readonly byte[] Source;

            public int SourceOffset;

            public int SourceCount;

            public readonly byte[] Des;

            public int DesOffset;

            public int DesCount;

            public ArrayItem(byte[] source, int sourceOffset, int sourceCount, byte[] des, int desOffset, int desCount)
            {
                Source = source;
                SourceOffset = sourceOffset;
                SourceCount = sourceCount;
                Des = des;
                DesOffset = desOffset;
                DesCount = desCount;
            }
        }

        struct WriteCopy : ICopy
        {

            public int Copy(Span<byte> source, Span<byte> des)
            {
                return CopyTo(des, source);
            }
        }

        struct ReadCopy : ICopy
        {
            public int Copy(Span<byte> source, Span<byte> des)
            {
                return CopyTo(source, des);
            }
        }

        static int CopyTo(ReadOnlySpan<byte> source, Span<byte> des)
        {

            int n;
            if (source.Length > des.Length)
            {
                n = des.Length;
            }
            else
            {
                n = source.Length;
            }


            source.Slice(0, n).CopyTo(des);

            return n;
        }


        static void Add(ref ArrayItem item, int n)
        {
            item.SourceOffset += n;

            item.SourceCount -= n;

            item.DesOffset += n;

            item.DesCount -= n;
        }

        static int Copy_Copy<T>(ref ArrayItem item, T copy) where T : ICopy
        {
            int n = copy.Copy(
                item.Source.AsSpan(item.SourceOffset, item.SourceCount),
                item.Des.AsSpan(item.DesOffset, item.DesCount));

            Add(ref item, n);

            return n;
        }

        static int Copy<T>(ref ArrayItem item, T copy) where T : ICopy
        {
            int allCount = item.SourceOffset + item.SourceCount;

            if (allCount < item.Source.Length)
            {
                return Copy_Copy(ref item, copy);
            }
            else
            {
                int n = copy.Copy(
                    item.Source.AsSpan(item.SourceOffset, item.Source.Length - item.SourceOffset),
                    item.Des.AsSpan(item.DesOffset, item.DesCount));

                Add(ref item, n);

                if (item.SourceOffset == item.Source.Length)
                {
                    item.SourceOffset = 0;

                    return n + Copy_Copy(ref item, copy);
                }
                else
                {
                    return n;
                }
            }
        }


        public static int Read(ref ArrayItem item)
        {
            return Copy(ref item, new ReadCopy());
        }

        public static int Write(ref ArrayItem item)
        {
            return Copy(ref item, new WriteCopy());
        }
    }


    struct BufferLoop
    {
        readonly byte[] _buffer;

        int _readOffset;

        int _readCount;

        int _writeOffset;

        int _writeCount;

        public int CanReadCount => _readCount;

        public int CanWriteCount => _writeCount;

        public BufferLoop(object obj)
        {
            _buffer = new byte[ushort.MaxValue];

            _readCount = 0;

            _readOffset = 0;

            _writeCount = _buffer.Length;

            _writeOffset = 0;
        }


        public int Write(byte[] buffer, int offset, int count)
        {
            new ArraySegment<byte>(buffer, offset, count);



            var item = new BufferMethod.ArrayItem(
                _buffer, _writeOffset, _writeCount,
                buffer, offset, count);

            int n = BufferMethod.Write(ref item);

            _writeOffset = item.SourceOffset;

            _writeCount = item.SourceCount;

            _readCount += n;

            return n;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            new ArraySegment<byte>(buffer, offset, count);

            var item = new BufferMethod.ArrayItem(
                _buffer, _readOffset, _readCount,
                buffer, offset, count);


            int n = BufferMethod.Read(ref item);


            _readOffset = item.SourceOffset;

            _readCount = item.SourceCount;

            _writeCount += n;



            return n;
        }
    }




    static class QueueExtensions
    {
        public static bool TryPeek<T>(this Queue<T> queue, out T item)
        {
            if (queue.Count == 0)
            {
                item = default;

                return false;
            }
            else
            {
                item = queue.Peek();

                return true;
            }

        }

        public static bool TryDequeue<T>(this Queue<T> queue, out T item)
        {
            if (queue.Count == 0)
            {
                item = default;

                return false;
            }
            else
            {
                item = queue.Dequeue();

                return true;
            }
        }
    }

    static class ExceptionSource
    {
        public static SocketException Create(SocketError socketError)
        {
            return new SocketException((int)socketError);
        }
    }

    sealed class ReadBuffer
    {
        sealed class ReadItem
        {
            public ReadItem(byte[] array, int offset, int count)
            {
                Buffer = array ?? throw new ArgumentNullException(nameof(array));
                Offset = offset;
                Count = count;
            }


            public byte[] Buffer { get; }

            public int Offset { get; }

            public int Count { get; }

            TaskCompletionSource<int> _TaskCompletionSource;


            Task<int> _Task;


            public Task<int> GetTask()
            {
                if (_Task is null)
                {
                    _TaskCompletionSource = new TaskCompletionSource<int>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                    _Task = _TaskCompletionSource.Task;
                }

                return _Task;
            }


            public void Set(Exception exception)
            {
                _TaskCompletionSource.TrySetException(exception);
            }

            public void Set(int n)
            {
                if (_TaskCompletionSource is null)
                {
                    _Task = Task.FromResult(n);
                }
                else
                {
                    _TaskCompletionSource.TrySetResult(n);
                }
            }
        }

        readonly Queue<ReadItem> _readWaitQueue = new Queue<ReadItem>();

        readonly object _lock = new object();

        BufferLoop _buffer;

        uint _seq;

        bool _isClose;

        public ReadBuffer(uint seq)
        {
            _seq = seq;

            _buffer = new BufferLoop(default);

            _isClose = false;
        }





        void Read()
        {
            while (_readWaitQueue.TryPeek(out var item))
            {
                int n = _buffer.Read(item.Buffer, item.Offset, item.Count);

                if (n == 0)
                {
                    return;
                }
                else
                {
                    _readWaitQueue.Dequeue();

                    item.Set(n);
                }
            }
        }


        public void Close()
        {
            lock (_lock)
            {
                if (_isClose == false)
                {
                    _isClose = true;


                    while (_readWaitQueue.TryDequeue(out var item))
                    {
                        item.Set(ExceptionSource.Create(SocketError.Shutdown));
                    }

                }
            }
        }

        //乱序到达返回false
        public bool Write(ref uint seqRef, out ushort windowSizeRef, byte[] buffer, int offset, int count)
        {
            new ArraySegment<byte>(buffer, offset, count);

            uint seq = seqRef;

            ushort windowSize;

            bool ret;

            lock (_lock)
            {
                if (seq == _seq)
                {

                    var n = (uint)_buffer.Write(buffer, offset, count);

                    _seq += n;


                    seq = _seq;

                    windowSize = (ushort)_buffer.CanWriteCount;

                    ret = true;
                }
                else
                {

                    seq = _seq;


                    windowSize = (ushort)_buffer.CanWriteCount;

                    ret = false;

                }
            }


            seqRef = seq;


            windowSizeRef = windowSize;

            return ret;
        }

        public Task<int> Read(byte[] buffer, int offset, int count)
        {
            new ArraySegment<byte>(buffer, offset, count);

            if (count == 0)
            {
                return Task.FromResult(0);
            }


            lock (_lock)
            {
                if (_isClose)
                {
                    throw ExceptionSource.Create(SocketError.Shutdown);
                }

                var item = new ReadItem(buffer, offset, count);

                _readWaitQueue.Enqueue(item);

                Read();

                return item.GetTask();
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

        public DownPacket DownPacket()
        {
            DownPacket downPacket = new DownPacket(1024);

            downPacket.WriteTCP(
                    Quaternion,
                    TCPFlag.ACK | TCPFlag.SYN,
                    ushort.MaxValue,
                    SequenceNumber,
                    AcknowledgmentNumber,
                    default);


            return downPacket;

        }
    }


    //一开始C发送一个起始序号CX，确认序号无意义
    //我发一个确认序号CX+1， 发一个起始序号SX
    //然后C发送一个确认序号SX+1，发送一个起始序号CX+1
    //也就是说对面发送的是我确认的


    public sealed class TCPLayerInfo
    {

        

        public TCPLayerInfo(Func<byte[], int, int, int> read, Action<byte[], int, int> write, Action<TCPStream> intoConnect)
        {
            int mtuSize = 65535;

            if (mtuSize < 576 || mtuSize > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(mtuSize), "mtu太小或太大了，偏移会出错");
            }


            Read = read ?? throw new ArgumentNullException(nameof(read));
            Write = write ?? throw new ArgumentNullException(nameof(write));

            IntoConnect = intoConnect ?? throw new ArgumentNullException(nameof(intoConnect));

            Dic = new Dictionary<Quaternion, TCPStream>();

            HPDic = new Dictionary<Quaternion, HandshakePhase>();
        }


        internal Func<byte[], int, int, int> Read { get; }

        internal Action<byte[], int, int> Write { get; }


        internal Dictionary<Quaternion, TCPStream> Dic { get; }

        internal Dictionary<Quaternion, HandshakePhase> HPDic { get; }

        internal Action<TCPStream> IntoConnect { get; }
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

            Meth.CreateThreadAndRun(layer.Read, logAction);

            return layer;
        }



        internal void Write(DownPacket downPacket)
        {
            downPacket.WriteIPPacket(m_info.Write);
        }


        void Read()
        {
            UPPacket upPacket = new UPPacket(ushort.MaxValue);
            while (true)
            {

                if (upPacket.ReadIPPackeet(m_info.Read))
                {
                    UPPacket(upPacket);
                }
                else
                {

                    Console.WriteLine("已丢弃ip包");
                }
            }
        }


        void InitHandshake(UPPacket upPacket)
        {
            if (m_info.HPDic.Count > 6)
            {
                m_info.HPDic.Clear();

                Console.WriteLine("等待握手太多了");
            }


            if (m_info.HPDic.ContainsKey(upPacket.Quaternion))
            {
                m_info.HPDic.Remove(upPacket.Quaternion);

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

                Write(handshakePhase.DownPacket());

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


        void UPPacket(UPPacket packet)
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

       
        public UPPacket(int size)
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

        public DownPacket(int size)
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




    [StructLayout(LayoutKind.Auto)]
    public sealed class WriteUDPPacket
    {

        byte[] Array { get; }

        int Offset { get; set; }


        //Count代表的是是写入的有效字节的长度
        //而不是缓冲区可以使用的长度
        int Count { get; set; }

        public void Write(Action<byte[], int, int> action)
        {
            action(Array, Offset, Count);
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

        public void WriteUDP(Func<byte[], int, int, int> read)
        {
            int n = read(Array, Offset, Array.Length - Offset);

            Count += n;
        }

        public void WriteUDP(Quaternion quaternion)
        {
            
            Offset -= UDPHeader.HEADER_SIZE;

            Count += UDPHeader.HEADER_SIZE;

            ref var header = ref Meth.AsStruct<UDPHeader>(Array.AsSpan(Offset));

            UDPHeader.Set(
                ref header,
                quaternion.Source.Address,
                quaternion.Source.Port,
                quaternion.Des.Address,
                quaternion.Des.Port,
                Array.AsSpan(Offset, Count));

            WriteIPHeader(new IPData(quaternion.Source.Address, quaternion.Des.Address, Protocol.UDP));

        }

        void WriteIPHeader(IPData data)
        {
            int count = Count;

            Offset -= IPHeader.HEADER_SIZE;

            Count += IPHeader.HEADER_SIZE;

            ref IPHeader header = ref Meth.AsStruct<IPHeader>(Array.AsSpan(Offset));

            
            IPHeader.Set(ref header, data, (ushort)count);
        }

        public WriteUDPPacket(int size)
        {

            Array = new byte[size];

            InitOffsetCount();
        }


        public void InitOffsetCount()
        {
            //给标头预留空间免得复制缓冲区
            Offset = IPHeader.HEADER_SIZE + UDPHeader.HEADER_SIZE;

            Count = 0;
        }

    }








    [StructLayout(LayoutKind.Auto)]
    public sealed class ReadUDPPacket
    {

        public byte[] Array { get; }

        public int Offset { get; private set; }

        public int Count { get; private set; }

       

        public Quaternion Quaternion { get; private set; }
       
        public bool Read(Func<byte[], int, int, int> func)
        {
            int count = func(Array, 0, Array.Length);

            Offset = 0;

            Count = count;

            return ReadIPHeader();
        }

        bool ReadIPHeader()
        {
            ref IPHeader header = ref Meth.AsStruct<IPHeader>(Array.AsSpan(Offset));

            if (header.HeadLength != 5)
            {
                return false;    
            }
            else
            {
                
                Offset += IPHeader.HEADER_SIZE;

                Count -= IPHeader.HEADER_SIZE;

                if (header.Protocol == Protocol.UDP)
                {
                    return ReadUDPHeader(header.SourceAddress, header.DesAddress);

                }
                else
                {
                    return false;
                }

            }
        }


        bool ReadUDPHeader(IPv4Address source, IPv4Address des)
        {
            ref var header = ref Meth.AsStruct<UDPHeader>(Array.AsSpan(Offset));

            int headerSize = UDPHeader.HEADER_SIZE;

            Offset += headerSize;

            Count -= headerSize;

            Quaternion = new Quaternion(
                new IPv4EndPoint(source, header.SourcePort),
                new IPv4EndPoint(des, header.DesPort));

            return true;
        }


        public ReadUDPPacket(int size)
        {

            Array = new byte[size];

            Offset = 0;

            Count = 0;
        }
    }




    public static class AsTo
    {
        
        public static void As(Span<byte> buffer, Func<Quaternion, Quaternion> func)
        {
            ref var ip = ref Meth.AsStruct<IPHeader>(buffer);

            if (ip.Protocol == Protocol.UDP)
            {
                Console.WriteLine("UDP");

                return;
            }

            var tcpStart = buffer.Slice(IPHeader.HEADER_SIZE);

            ref var tcp = ref Meth.AsStruct<TCPHeader>(tcpStart);


            var que = func(new Quaternion(
                new IPv4EndPoint(ip.SourceAddress, tcp.SourcePort),
                new IPv4EndPoint(ip.DesAddress, tcp.DesPort)));


            TCPHeader.Set(
                ref tcp,
                que.Source.Address,
                que.Source.Port,
                que.Des.Address,
                que.Des.Port,
                tcpStart);


            IPHeader.Set(
                ref ip,
                que.Source.Address,
                que.Des.Address,
                Protocol.TCP);





        }
    }

}