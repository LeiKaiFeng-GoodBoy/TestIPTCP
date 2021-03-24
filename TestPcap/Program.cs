using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using LeiKaiFeng.TCPIP;
using PcapDotNet.Base;
using PcapDotNet.Core;
using PcapDotNet.Packets;
using PcapDotNet.Packets.Arp;
using PcapDotNet.Packets.Dns;
using PcapDotNet.Packets.Ethernet;
using PcapDotNet.Packets.Gre;
using PcapDotNet.Packets.Http;
using PcapDotNet.Packets.Icmp;
using PcapDotNet.Packets.Igmp;
using PcapDotNet.Packets.IpV4;
using PcapDotNet.Packets.IpV6;
using PcapDotNet.Packets.Transport;

namespace Test
{


    public sealed class MyLayer : Layer
    {
        byte[] m_bufffer;

        public MyLayer(byte[] buffer)
        {
            m_bufffer = buffer;
        }

        public override int Length => m_bufffer.Length;

        public override bool Equals(Layer other)
        {
            return object.ReferenceEquals(this, other);
        }

        public override void Write(byte[] buffer, int offset, int payloadLength, ILayer previousLayer, ILayer nextLayer)
        {
            Buffer.BlockCopy(m_bufffer, 0, buffer, offset, m_bufffer.Length);
        }
    }


    class Program
    {
        static void Main(string[] args)
        {

            // Take the selected adapter
            PacketDevice selectedDevice = LivePacketDevice.AllLocalMachine.First();

            // Open the output device
            using (PacketCommunicator communicator = selectedDevice.Open(100, // name of the device
                                                                         PacketDeviceOpenAttributes.Promiscuous, // promiscuous mode
                                                                         1000)) // read timeout
            {
                IPLayer layer = IPLayer.Init(new IPLayerInfo(
                (buffer, offset, count) => { Thread.Sleep(-1); return 0; },
                (buffer, offset, count) => {
                    BuildMy(communicator, buffer, offset, count);

                }));

                Quaternion quaternion = new Quaternion(new IPv4EndPoint(new IPv4Address(192, 168, 1, 106),
                        50), new IPv4EndPoint(new IPv4Address(192,168,1,1),
                        80)); 

                for (int i = 0; i < 100; i++)
                {

                    var packet = layer.CreateDownPacket();

                    packet.WriteTCP(
                        
                        quaternion,
                        TCPFlag.ACK | TCPFlag.SYN,
                        65535,
                        45,
                        4563434,
                        default);

                    layer.AddDownPacket(packet);
                }
            }

            Console.ReadLine();
        }

        static void BuildMy(PacketCommunicator communicator, byte[] buffer, int offset, int count)
        {

            EthernetLayer ethernetLayer =
                new EthernetLayer
                {
                    Source = new MacAddress("00:08:ca:c1:87:a9"),
                    Destination = new MacAddress("48:7d:2e:81:d1:37"),
                    EtherType = EthernetType.IpV4,
                };

            MyLayer myLayer = new MyLayer(buffer.AsSpan(offset, count).ToArray());

            PacketBuilder builder = new PacketBuilder(ethernetLayer, myLayer);

            communicator.SendPacket(builder.Build(DateTime.Now));
        }

        /// <summary>
        /// This function build an Ethernet with payload packet.
        /// </summary>
        private static Packet BuildEthernetPacket()
        {
            EthernetLayer ethernetLayer =
                new EthernetLayer
                {
                    Source = new MacAddress("01:01:01:01:01:01"),
                    Destination = new MacAddress("02:02:02:02:02:02"),
                    EtherType = EthernetType.IpV4,
                };

            PayloadLayer payloadLayer =
                new PayloadLayer
                {
                    Data = new Datagram(Encoding.ASCII.GetBytes("hello world")),
                };

            PacketBuilder builder = new PacketBuilder(ethernetLayer, payloadLayer);

            return builder.Build(DateTime.Now);
        }

        /// <summary>
        /// This function build an ARP over Ethernet packet.
        /// </summary>
        private static Packet BuildArpPacket()
        {
            EthernetLayer ethernetLayer =
                new EthernetLayer
                {
                    Source = new MacAddress("01:01:01:01:01:01"),
                    Destination = new MacAddress("02:02:02:02:02:02"),
                    EtherType = EthernetType.None, // Will be filled automatically.
                };

            ArpLayer arpLayer =
                new ArpLayer
                {
                    ProtocolType = EthernetType.IpV4,
                    Operation = ArpOperation.Request,
                    SenderHardwareAddress = new byte[] { 3, 3, 3, 3, 3, 3 }.AsReadOnly(), // 03:03:03:03:03:03.
                    SenderProtocolAddress = new byte[] { 1, 2, 3, 4 }.AsReadOnly(), // 1.2.3.4.
                    TargetHardwareAddress = new byte[] { 4, 4, 4, 4, 4, 4 }.AsReadOnly(), // 04:04:04:04:04:04.
                    TargetProtocolAddress = new byte[] { 11, 22, 33, 44 }.AsReadOnly(), // 11.22.33.44.
                };

            PacketBuilder builder = new PacketBuilder(ethernetLayer, arpLayer);

            return builder.Build(DateTime.Now);
        }

        /// <summary>
        /// This function build a VLanTaggedFrame over Ethernet with payload packet.
        /// </summary>
        private static Packet BuildVLanTaggedFramePacket()
        {
            EthernetLayer ethernetLayer =
                new EthernetLayer
                {
                    Source = new MacAddress("01:01:01:01:01:01"),
                    Destination = new MacAddress("02:02:02:02:02:02"),
                    EtherType = EthernetType.None, // Will be filled automatically.
                };

            VLanTaggedFrameLayer vLanTaggedFrameLayer =
                new VLanTaggedFrameLayer
                {
                    PriorityCodePoint = ClassOfService.Background,
                    CanonicalFormatIndicator = false,
                    VLanIdentifier = 50,
                    EtherType = EthernetType.IpV4,
                };

            PayloadLayer payloadLayer =
                new PayloadLayer
                {
                    Data = new Datagram(Encoding.ASCII.GetBytes("hello world")),
                };

            PacketBuilder builder = new PacketBuilder(ethernetLayer, vLanTaggedFrameLayer, payloadLayer);

            return builder.Build(DateTime.Now);
        }

        /// <summary>
        /// This function build an IPv4 over Ethernet with payload packet.
        /// </summary>
        private static Packet BuildIpV4Packet()
        {
           
            EthernetLayer ethernetLayer =
                new EthernetLayer
                {
                    
                    Source = new MacAddress("01:01:01:01:01:01"),
                    Destination = new MacAddress("02:02:02:02:02:02"),
                    EtherType = EthernetType.None,
                };

            IpV4Layer ipV4Layer =
                new IpV4Layer
                {
                    Source = new IpV4Address("1.2.3.4"),
                    CurrentDestination = new IpV4Address("11.22.33.44"),
                    Fragmentation = IpV4Fragmentation.None,
                    HeaderChecksum = null, // Will be filled automatically.
                    Identification = 123,
                    Options = IpV4Options.None,
                    Protocol = IpV4Protocol.Udp,
                    Ttl = 100,
                    TypeOfService = 0,
                };

            PayloadLayer payloadLayer =
                new PayloadLayer
                {
                    Data = new Datagram(Encoding.ASCII.GetBytes("hello world")),
                };

            PacketBuilder builder = new PacketBuilder(ethernetLayer, ipV4Layer, payloadLayer);

            return builder.Build(DateTime.Now);
        }

        /// <summary>
        /// This function build an IPv6 over Ethernet with payload packet.
        /// </summary>
        private static Packet BuildIpV6Packet()
        {
            EthernetLayer ethernetLayer =
                new EthernetLayer
                {
                    Source = new MacAddress("01:01:01:01:01:01"),
                    Destination = new MacAddress("02:02:02:02:02:02"),
                    EtherType = EthernetType.None,
                };

            IpV6Layer ipV6Layer =
                new IpV6Layer
                {
                    Source = new IpV6Address("0123:4567:89AB:CDEF:0123:4567:89AB:CDEF"),
                    CurrentDestination = new IpV6Address("FEDC:BA98:7654:3210:FEDC:BA98:7654:3210"),
                    FlowLabel = 123,
                    HopLimit = 100,
                    NextHeader = IpV4Protocol.Udp,
                };

            PayloadLayer payloadLayer =
                new PayloadLayer
                {
                    Data = new Datagram(Encoding.ASCII.GetBytes("hello world")),
                };

            PacketBuilder builder = new PacketBuilder(ethernetLayer, ipV6Layer, payloadLayer);

            return builder.Build(DateTime.Now);
        }

        /// <summary>
        /// This function build an ICMP over IPv4 over Ethernet packet.
        /// </summary>
        private static Packet BuildIcmpPacket()
        {
            EthernetLayer ethernetLayer =
                new EthernetLayer
                {
                    Source = new MacAddress("01:01:01:01:01:01"),
                    Destination = new MacAddress("02:02:02:02:02:02"),
                    EtherType = EthernetType.None, // Will be filled automatically.
                };

            IpV4Layer ipV4Layer =
                new IpV4Layer
                {
                    Source = new IpV4Address("1.2.3.4"),
                    CurrentDestination = new IpV4Address("11.22.33.44"),
                    Fragmentation = IpV4Fragmentation.None,
                    HeaderChecksum = null, // Will be filled automatically.
                    Identification = 123,
                    Options = IpV4Options.None,
                    Protocol = null, // Will be filled automatically.
                    Ttl = 100,
                    TypeOfService = 0,
                };

            IcmpEchoLayer icmpLayer =
                new IcmpEchoLayer
                {
                    Checksum = null, // Will be filled automatically.
                    Identifier = 456,
                    SequenceNumber = 800,
                };

            PacketBuilder builder = new PacketBuilder(ethernetLayer, ipV4Layer, icmpLayer);

            return builder.Build(DateTime.Now);
        }

        /// <summary>
        /// This function build an IGMP over IPv4 over Ethernet packet.
        /// </summary>
        private static Packet BuildIgmpPacket()
        {
            EthernetLayer ethernetLayer =
                new EthernetLayer
                {
                    Source = new MacAddress("01:01:01:01:01:01"),
                    Destination = new MacAddress("02:02:02:02:02:02"),
                    EtherType = EthernetType.None, // Will be filled automatically.
                };

            IpV4Layer ipV4Layer =
                new IpV4Layer
                {
                    Source = new IpV4Address("1.2.3.4"),
                    CurrentDestination = new IpV4Address("11.22.33.44"),
                    Fragmentation = IpV4Fragmentation.None,
                    HeaderChecksum = null, // Will be filled automatically.
                    Identification = 123,
                    Options = IpV4Options.None,
                    Protocol = null, // Will be filled automatically.
                    Ttl = 100,
                    TypeOfService = 0,
                };

            IgmpQueryVersion1Layer igmpLayer =
                new IgmpQueryVersion1Layer
                {
                    GroupAddress = new IpV4Address("1.2.3.4"),
                };

            PacketBuilder builder = new PacketBuilder(ethernetLayer, ipV4Layer, igmpLayer);

            return builder.Build(DateTime.Now);
        }

        /// <summary>
        /// This function build an IPv4 over GRE over IPv4 over Ethernet packet.
        /// </summary>
        private static Packet BuildGrePacket()
        {
            EthernetLayer ethernetLayer =
                new EthernetLayer
                {
                    Source = new MacAddress("01:01:01:01:01:01"),
                    Destination = new MacAddress("02:02:02:02:02:02"),
                    EtherType = EthernetType.None, // Will be filled automatically.
                };

            IpV4Layer ipV4Layer =
                new IpV4Layer
                {
                    Source = new IpV4Address("1.2.3.4"),
                    CurrentDestination = new IpV4Address("11.22.33.44"),
                    Fragmentation = IpV4Fragmentation.None,
                    HeaderChecksum = null, // Will be filled automatically.
                    Identification = 123,
                    Options = IpV4Options.None,
                    Protocol = null, // Will be filled automatically.
                    Ttl = 100,
                    TypeOfService = 0,
                };

            GreLayer greLayer =
                new GreLayer
                {
                    Version = GreVersion.Gre,
                    ProtocolType = EthernetType.None, // Will be filled automatically.
                    RecursionControl = 0,
                    FutureUseBits = 0,
                    ChecksumPresent = true,
                    Checksum = null, // Will be filled automatically.
                    Key = null,
                    SequenceNumber = 123,
                    AcknowledgmentSequenceNumber = null,
                    RoutingOffset = null,
                    Routing = null,
                    StrictSourceRoute = false,
                };

            IpV4Layer innerIpV4Layer =
                new IpV4Layer
                {
                    Source = new IpV4Address("100.200.201.202"),
                    CurrentDestination = new IpV4Address("123.254.132.40"),
                    Fragmentation = IpV4Fragmentation.None,
                    HeaderChecksum = null, // Will be filled automatically.
                    Identification = 123,
                    Options = IpV4Options.None,
                    Protocol = IpV4Protocol.Udp,
                    Ttl = 120,
                    TypeOfService = 0,
                };

            PacketBuilder builder = new PacketBuilder(ethernetLayer, ipV4Layer, greLayer, innerIpV4Layer);

            return builder.Build(DateTime.Now);
        }

        /// <summary>
        /// This function build an UDP over IPv4 over Ethernet with payload packet.
        /// </summary>
        private static Packet BuildUdpPacket()
        {
            EthernetLayer ethernetLayer =
                new EthernetLayer
                {
                    Source = new MacAddress("01:01:01:01:01:01"),
                    Destination = new MacAddress("02:02:02:02:02:02"),
                    EtherType = EthernetType.None, // Will be filled automatically.
                };

            IpV4Layer ipV4Layer =
                new IpV4Layer
                {
                    Source = new IpV4Address("1.2.3.4"),
                    CurrentDestination = new IpV4Address("11.22.33.44"),
                    Fragmentation = IpV4Fragmentation.None,
                    HeaderChecksum = null, // Will be filled automatically.
                    Identification = 123,
                    Options = IpV4Options.None,
                    Protocol = null, // Will be filled automatically.
                    Ttl = 100,
                    TypeOfService = 0,
                };

            UdpLayer udpLayer =
                new UdpLayer
                {
                    SourcePort = 4050,
                    DestinationPort = 25,
                    Checksum = null, // Will be filled automatically.
                    CalculateChecksumValue = true,
                };

            PayloadLayer payloadLayer =
                new PayloadLayer
                {
                    Data = new Datagram(Encoding.ASCII.GetBytes("hello world")),
                };

            PacketBuilder builder = new PacketBuilder(ethernetLayer, ipV4Layer, udpLayer, payloadLayer);

            return builder.Build(DateTime.Now);
        }

        /// <summary>
        /// This function build an TCP over IPv4 over Ethernet with payload packet.
        /// </summary>
        private static Packet BuildTcpPacket()
        {
            EthernetLayer ethernetLayer =
                new EthernetLayer
                {
                    Source = new MacAddress("01:01:01:01:01:01"),
                    Destination = new MacAddress("02:02:02:02:02:02"),
                    EtherType = EthernetType.None, // Will be filled automatically.
                };

            IpV4Layer ipV4Layer =
                new IpV4Layer
                {
                    Source = new IpV4Address("1.2.3.4"),
                    CurrentDestination = new IpV4Address("11.22.33.44"),
                    Fragmentation = IpV4Fragmentation.None,
                    HeaderChecksum = null, // Will be filled automatically.
                    Identification = 123,
                    Options = IpV4Options.None,
                    Protocol = null, // Will be filled automatically.
                    Ttl = 100,
                    TypeOfService = 0,
                };

            TcpLayer tcpLayer =
                new TcpLayer
                {
                    SourcePort = 4050,
                    DestinationPort = 25,
                    Checksum = null, // Will be filled automatically.
                    SequenceNumber = 100,
                    AcknowledgmentNumber = 50,
                    ControlBits = TcpControlBits.Acknowledgment,
                    Window = 100,
                    UrgentPointer = 0,
                    Options = TcpOptions.None,
                };

            PayloadLayer payloadLayer =
                new PayloadLayer
                {
                    Data = new Datagram(Encoding.ASCII.GetBytes("hello world")),
                };

            PacketBuilder builder = new PacketBuilder(ethernetLayer, ipV4Layer, tcpLayer, payloadLayer);

            return builder.Build(DateTime.Now);
        }

        /// <summary>
        /// This function build a DNS over UDP over IPv4 over Ethernet packet.
        /// </summary>
        private static Packet BuildDnsPacket()
        {
            EthernetLayer ethernetLayer =
                new EthernetLayer
                {
                    Source = new MacAddress("01:01:01:01:01:01"),
                    Destination = new MacAddress("02:02:02:02:02:02"),
                    EtherType = EthernetType.None, // Will be filled automatically.
                };

            IpV4Layer ipV4Layer =
                new IpV4Layer
                {
                    Source = new IpV4Address("1.2.3.4"),
                    CurrentDestination = new IpV4Address("11.22.33.44"),
                    Fragmentation = IpV4Fragmentation.None,
                    HeaderChecksum = null, // Will be filled automatically.
                    Identification = 123,
                    Options = IpV4Options.None,
                    Protocol = null, // Will be filled automatically.
                    Ttl = 100,
                    TypeOfService = 0,
                };

            UdpLayer udpLayer =
                new UdpLayer
                {
                    SourcePort = 4050,
                    DestinationPort = 53,
                    Checksum = null, // Will be filled automatically.
                    CalculateChecksumValue = true,
                };

            DnsLayer dnsLayer =
                new DnsLayer
                {
                    Id = 100,
                    IsResponse = false,
                    OpCode = DnsOpCode.Query,
                    IsAuthoritativeAnswer = false,
                    IsTruncated = false,
                    IsRecursionDesired = true,
                    IsRecursionAvailable = false,
                    FutureUse = false,
                    IsAuthenticData = false,
                    IsCheckingDisabled = false,
                    ResponseCode = DnsResponseCode.NoError,
                    Queries = new[]
                                      {
                                          new DnsQueryResourceRecord(new DnsDomainName("pcapdot.net"),
                                                                     DnsType.A,
                                                                     DnsClass.Internet),
                                      },
                    Answers = null,
                    Authorities = null,
                    Additionals = null,
                    DomainNameCompressionMode = DnsDomainNameCompressionMode.All,
                };

            PacketBuilder builder = new PacketBuilder(ethernetLayer, ipV4Layer, udpLayer, dnsLayer);

            return builder.Build(DateTime.Now);
        }

        /// <summary>
        /// This function build an HTTP over TCP over IPv4 over Ethernet packet.
        /// </summary>
        private static Packet BuildHttpPacket()
        {
            EthernetLayer ethernetLayer =
                new EthernetLayer
                {
                    Source = new MacAddress("01:01:01:01:01:01"),
                    Destination = new MacAddress("02:02:02:02:02:02"),
                    EtherType = EthernetType.None, // Will be filled automatically.
                };

            IpV4Layer ipV4Layer =
                new IpV4Layer
                {
                    Source = new IpV4Address("1.2.3.4"),
                    CurrentDestination = new IpV4Address("11.22.33.44"),
                    Fragmentation = IpV4Fragmentation.None,
                    HeaderChecksum = null, // Will be filled automatically.
                    Identification = 123,
                    Options = IpV4Options.None,
                    Protocol = null, // Will be filled automatically.
                    Ttl = 100,
                    TypeOfService = 0,
                };

            TcpLayer tcpLayer =
                new TcpLayer
                {
                    SourcePort = 4050,
                    DestinationPort = 80,
                    Checksum = null, // Will be filled automatically.
                    SequenceNumber = 100,
                    AcknowledgmentNumber = 50,
                    ControlBits = TcpControlBits.Acknowledgment,
                    Window = 100,
                    UrgentPointer = 0,
                    Options = TcpOptions.None,
                };

            HttpRequestLayer httpLayer =
                new HttpRequestLayer
                {
                    Version = PcapDotNet.Packets.Http.HttpVersion.Version11,
                    Header = new HttpHeader(new HttpContentLengthField(11)),
                    Body = new Datagram(Encoding.ASCII.GetBytes("hello world")),
                    Method = new HttpRequestMethod(HttpRequestKnownMethod.Get),
                    Uri = @"http://pcapdot.net/",
                };

            PacketBuilder builder = new PacketBuilder(ethernetLayer, ipV4Layer, tcpLayer, httpLayer);

            return builder.Build(DateTime.Now);
        }

        /// <summary>
        /// This function build a DNS over UDP over IPv4 over GRE over IPv4 over IPv4 over VLAN Tagged Frame over VLAN Tagged Frame over Ethernet.
        /// </summary>
        private static Packet BuildComplexPacket()
        {
            return PacketBuilder.Build(
                DateTime.Now,
                new EthernetLayer
                {
                    Source = new MacAddress("01:01:01:01:01:01"),
                    Destination = new MacAddress("02:02:02:02:02:02"),
                    EtherType = EthernetType.None, // Will be filled automatically.
                },
                new VLanTaggedFrameLayer
                {
                    PriorityCodePoint = ClassOfService.ExcellentEffort,
                    CanonicalFormatIndicator = false,
                    EtherType = EthernetType.None, // Will be filled automatically.
                },
                new VLanTaggedFrameLayer
                {
                    PriorityCodePoint = ClassOfService.BestEffort,
                    CanonicalFormatIndicator = false,
                    EtherType = EthernetType.None, // Will be filled automatically.
                },
                new IpV4Layer
                {
                    Source = new IpV4Address("1.2.3.4"),
                    CurrentDestination = new IpV4Address("11.22.33.44"),
                    Fragmentation = IpV4Fragmentation.None,
                    HeaderChecksum = null, // Will be filled automatically.
                    Identification = 123,
                    Options = IpV4Options.None,
                    Protocol = null, // Will be filled automatically.
                    Ttl = 100,
                    TypeOfService = 0,
                },
                new IpV4Layer
                {
                    Source = new IpV4Address("5.6.7.8"),
                    CurrentDestination = new IpV4Address("55.66.77.88"),
                    Fragmentation = IpV4Fragmentation.None,
                    HeaderChecksum = null, // Will be filled automatically.
                    Identification = 456,
                    Options = new IpV4Options(new IpV4OptionStrictSourceRouting(
                                                      new[]
                                                          {
                                                              new IpV4Address("100.200.100.200"),
                                                              new IpV4Address("150.250.150.250")
                                                          }, 1)),
                    Protocol = null, // Will be filled automatically.
                    Ttl = 200,
                    TypeOfService = 0,
                },
                new GreLayer
                {
                    Version = GreVersion.Gre,
                    ProtocolType = EthernetType.None, // Will be filled automatically.
                    RecursionControl = 0,
                    FutureUseBits = 0,
                    ChecksumPresent = true,
                    Checksum = null, // Will be filled automatically.
                    Key = 100,
                    SequenceNumber = 123,
                    AcknowledgmentSequenceNumber = null,
                    RoutingOffset = null,
                    Routing = new[]
                                      {
                                          new GreSourceRouteEntryIp(
                                              new[]
                                                  {
                                                      new IpV4Address("10.20.30.40"),
                                                      new IpV4Address("40.30.20.10")
                                                  }.AsReadOnly(), 1),
                                          new GreSourceRouteEntryIp(
                                              new[]
                                                  {
                                                      new IpV4Address("11.22.33.44"),
                                                      new IpV4Address("44.33.22.11")
                                                  }.AsReadOnly(), 0)
                                      }.Cast<GreSourceRouteEntry>().ToArray().AsReadOnly(),
                    StrictSourceRoute = false,
                },
                new IpV4Layer
                {
                    Source = new IpV4Address("51.52.53.54"),
                    CurrentDestination = new IpV4Address("61.62.63.64"),
                    Fragmentation = IpV4Fragmentation.None,
                    HeaderChecksum = null, // Will be filled automatically.
                    Identification = 123,
                    Options = new IpV4Options(
                            new IpV4OptionTimestampOnly(0, 1,
                                                        new IpV4TimeOfDay(new TimeSpan(1, 2, 3)),
                                                        new IpV4TimeOfDay(new TimeSpan(15, 55, 59))),
                            new IpV4OptionQuickStart(IpV4OptionQuickStartFunction.RateRequest, 10, 200, 300)),
                    Protocol = null, // Will be filled automatically.
                    Ttl = 100,
                    TypeOfService = 0,
                },
                new UdpLayer
                {
                    SourcePort = 53,
                    DestinationPort = 40101,
                    Checksum = null, // Will be filled automatically.
                    CalculateChecksumValue = true,
                },
                new DnsLayer
                {
                    Id = 10012,
                    IsResponse = true,
                    OpCode = DnsOpCode.Query,
                    IsAuthoritativeAnswer = true,
                    IsTruncated = false,
                    IsRecursionDesired = true,
                    IsRecursionAvailable = true,
                    FutureUse = false,
                    IsAuthenticData = true,
                    IsCheckingDisabled = false,
                    ResponseCode = DnsResponseCode.NoError,
                    Queries =
                            new[]
                                {
                                    new DnsQueryResourceRecord(
                                        new DnsDomainName("pcapdot.net"),
                                        DnsType.Any,
                                        DnsClass.Internet),
                                },
                    Answers =
                            new[]
                                {
                                    new DnsDataResourceRecord(
                                        new DnsDomainName("pcapdot.net"),
                                        DnsType.A,
                                        DnsClass.Internet
                                        , 50000,
                                        new DnsResourceDataIpV4(new IpV4Address("10.20.30.44"))),
                                    new DnsDataResourceRecord(
                                        new DnsDomainName("pcapdot.net"),
                                        DnsType.Txt,
                                        DnsClass.Internet,
                                        50000,
                                        new DnsResourceDataText(new[] {new DataSegment(Encoding.ASCII.GetBytes("Pcap.Net"))}.AsReadOnly()))
                                },
                    Authorities =
                            new[]
                                {
                                    new DnsDataResourceRecord(
                                        new DnsDomainName("pcapdot.net"),
                                        DnsType.MailExchange,
                                        DnsClass.Internet,
                                        100,
                                        new DnsResourceDataMailExchange(100, new DnsDomainName("pcapdot.net")))
                                },
                    Additionals =
                            new[]
                                {
                                    new DnsOptResourceRecord(
                                        new DnsDomainName("pcapdot.net"),
                                        50000,
                                        0,
                                        DnsOptVersion.Version0,
                                        DnsOptFlags.DnsSecOk,
                                        new DnsResourceDataOptions(
                                            new DnsOptions(
                                                new DnsOptionUpdateLease(100),
                                                new DnsOptionLongLivedQuery(1,
                                                                            DnsLongLivedQueryOpCode.Refresh,
                                                                            DnsLongLivedQueryErrorCode.NoError,
                                                                            10, 20))))
                                },
                    DomainNameCompressionMode = DnsDomainNameCompressionMode.All,
                });
        }
    }
}