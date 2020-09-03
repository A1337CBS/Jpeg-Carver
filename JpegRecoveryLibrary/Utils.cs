using PacketDotNet.Connections;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JpegRecoveryLibrary
{
    class Utils
    {
        static PreCheck preCheck = new PreCheck();

        public long recoverSegment(string file, Program p)
        {
            long recLength = 0;
            try
            {
                int dhtID = 1;
                Huffman.switchHuffman(dhtID);
                Console.WriteLine("HuffmanID" + dhtID);
                if (p.startDecoding())
                {
                    //first dht is ok
                    return p.finalizeDecoding();
                }

                Program p2to7 = new Program(file);
                Huffman.switchHuffman(2);
                int maxdhtID = Huffman.maxDhtID;
                while (!p2to7.startDecoding() && dhtID < 8)
                {
                    dhtID++;
                    Console.WriteLine("HuffmanID" + dhtID);
                    Huffman.switchHuffman(dhtID);
                    p = new Program(file);
                }
                // if all is consumed, then, return 1st dht (the standard table)
                recLength = (dhtID == 8) ? p.finalizeDecoding() : p2to7.finalizeDecoding();
            }
            catch (Exception e) { }
            return recLength;
        }

        public void recoverHuffmanHdr(Stream fileStream)
        {
            //Check if file is jpeg or not
            //bool isJpeg = preCheck.isJpeg(testFileStream);
            //Console.WriteLine("Is Jpeg File?: " + isJpeg);

            //Check if jpeg partial headers exist
            //1st, Find SOS marker
            //fileStream.Seek(0, SeekOrigin.Begin);
            //Reading stream changes position, retain original
            long cpoint = fileStream.Position;
            var sos = preCheck.get_sos_cnt_and_point(fileStream);
            fileStream.Position = cpoint;

            int sos_index = sos.Item1; // which SOS code is hit
            long sos_point = sos.Item2; // point of encoded data starts. go ahead and recover consequent jpeg
            if (sos_index == -1)
            {
                Console.WriteLine("SOS marker not found");
            }
            else
            {
                Console.WriteLine("SOS marker exists at:" + sos_point);
            }


            //Find DHT marker

            /*DHT
             * FF C4 - Marker(2 bytes)
             * XX XX - Length(2 bytes)
             * XX    - Table info(1 byte) Upper nibble = 0/1 (DC=0/AC=1), Lower nibble = 0-3 Table ID
             * [16]  - # of codes of each length
             * [X]   - Symbols
             */


            //If SOS exists then Check if DHT exists
            if (sos_index == 0)
            {
                cpoint = fileStream.Position;
                var dht = preCheck.get_dht_point(fileStream);
                fileStream.Position = cpoint;
                int dht_index = dht.Item1; // DHT is hit or not
                long dht_point = dht.Item2; // point of DHT data starts.
                if (dht_index == -1)
                {
                    Console.WriteLine("DHT marker not found");
                }
                else
                {
                    Console.WriteLine("DHT marker starts at:" + dht_point);
                    cpoint = fileStream.Position;
                    preCheck.parseDht(fileStream, dht_point);
                }
            }
            fileStream.Position = cpoint;

        }

        public List<MemoryStream> packetParser(String capFile)
        {
            //Open packet file
            //String capFile = "";
            ICaptureDevice device;

            try
            {
                // Get an offline device
                device = new CaptureFileReaderDevice(capFile);
                // tcpdump filter to capture only TCP/IP packets
                string filter = "ip and (tcp or udp)";
                //Convert to flows
                //string tcpFilter = "tcp.stream eq 1";
                //string udpFilter = "udp.stream==1";

                device.Filter = filter;

                // Open the device
                device.Open();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error - Caught exception when opening file" + e.ToString());
                return null;
            }




            RawCapture packets;
            List<byte> bytesList = new List<byte>();
            List<NetFlow> tcpNetFlows = new List<NetFlow>();
            List<NetFlow> udpNetFlows = new List<NetFlow>();


            // Capture packets using GetNextPacket()
            while ((packets = device.GetNextPacket()) != null)
            {
                // Prints the time and length of each received packet
                var time = packets.Timeval.Date;
                var len = packets.Data.Length;
                // var tcpPacket = packet.Data.
                //Console.WriteLine("{0}:{1}:{2},{3} Len={4}", time.Hour, time.Minute, time.Second, time.Millisecond, len);
                // use PacketDotNet to parse this packet and print out
                // its high level information

                var p = PacketDotNet.Packet.ParsePacket(packets.LinkLayerType, packets.Data);
                var tcpPacket = p.Extract<PacketDotNet.TcpPacket>();
                var udpPacket = p.Extract<PacketDotNet.UdpPacket>();

                //tcpPacket, also requires assembling packets into proper sequences
                if (tcpPacket != null)
                {

                    var ipPacket = (PacketDotNet.IPPacket)tcpPacket.ParentPacket;
                    System.Net.IPAddress srcIp = ipPacket.SourceAddress;
                    System.Net.IPAddress dstIp = ipPacket.DestinationAddress;
                    int srcPort = tcpPacket.SourcePort;
                    int dstPort = tcpPacket.DestinationPort;

                    Console.WriteLine("TCP {0}:{1}:{2},{3} Len={4} {5}:{6} -> {7}:{8}",
                        time.Hour, time.Minute, time.Second, time.Millisecond, len,
                        srcIp, srcPort, dstIp, dstPort);

                    if (tcpPacket.HasPayloadData && tcpPacket.PayloadData.Length > 0)
                    {
                        //Create a NetFlow
                        NetFlow currentNetFlow = new NetFlow(srcIp, dstIp, srcPort, dstPort);
                        if (tcpNetFlows.Contains(currentNetFlow))
                        {
                            int index = tcpNetFlows.IndexOf(currentNetFlow);
                            currentNetFlow = tcpNetFlows[index];
                            if (!currentNetFlow.isPresentSeqNum(tcpPacket.SequenceNumber))
                            {
                                currentNetFlow.addPacketData(tcpPacket.SequenceNumber, tcpPacket.PayloadData);
                            }
                        }
                        else
                        {
                            currentNetFlow.addPacketData(tcpPacket.SequenceNumber, tcpPacket.PayloadData);
                            tcpNetFlows.Add(currentNetFlow);
                        }

                        //foreach (byte data in tcpPacket.PayloadData) {
                        //    bytesList.Add(data);
                        //}
                    }



                }
                //udpPacket
                if (udpPacket != null)
                {

                    if (udpPacket.HasPayloadData && udpPacket.PayloadData.Length > 0)
                    {
                        var ipPacket = (PacketDotNet.IPPacket)udpPacket.ParentPacket;
                        System.Net.IPAddress srcIp = ipPacket.SourceAddress;
                        System.Net.IPAddress dstIp = ipPacket.DestinationAddress;
                        int srcPort = udpPacket.SourcePort;
                        int dstPort = udpPacket.DestinationPort;
                        //Create a NetFlow
                        NetFlow currentNetFlow = new NetFlow(srcIp, dstIp, srcPort, dstPort);
                        if (udpNetFlows.Contains(currentNetFlow))
                        {
                            int index = udpNetFlows.IndexOf(currentNetFlow);
                            currentNetFlow = udpNetFlows[index];
                            uint seqNum = (uint)currentNetFlow.payloadDataCount();
                            currentNetFlow.addPacketData(seqNum, udpPacket.PayloadData);
                        }
                        else
                        {
                            currentNetFlow.addPacketData(0, udpPacket.PayloadData);
                            udpNetFlows.Add(currentNetFlow);
                        }

                        //foreach (byte data in udpPacket.PayloadData)
                        //{
                        //    bytesList.Add(data);
                        //}
                    }
                }

                //Console.WriteLine(p.ToString());

            }

            // Print out the device statistics
            //Console.WriteLine(device.Statistics.ToString());

            //Close the pcap device
            device.Close();

            //MemoryStream memoryStream = new MemoryStream(bytesList.ToArray());
            List<MemoryStream> memoryStreams = new List<MemoryStream>();
            for (int i = 0; i < tcpNetFlows.Count; i++)
            {
                memoryStreams.Add(tcpNetFlows[i].getAssembledFlowData());
            }
            for (int j = 0; j < udpNetFlows.Count; j++)
            {
                memoryStreams.Add(udpNetFlows[j].getAssembledFlowData());
            }



            return memoryStreams;

            /*
            Packet wpacket = Packet.ParsePacket();
            // wpacket = received packet

            UdpDatagram udp = null;
            TcpDatagram tcp = null;
            Datagram datagram = null;

            IpV4Datagram ip4 = wpacket.Ethernet.IpV4;
            if (ip4.Protocol == IpV4Protocol.Udp)
            {
                udp = ip4.Udp;
                datagram = udp.Payload;
            }
            if (ip4.Protocol == IpV4Protocol.Tcp)
            {
                tcp = ip4.Tcp;
                datagram = tcp.Payload;
            }
            if (null != datagram)
            {
                int payloadLength = datagram.Length;
                using (MemoryStream ms = datagram.ToMemoryStream())
                {
                    byte[] rx_payload = new byte[payloadLength];
                    ms.Read(rx_payload, 0, payloadLength);
                }
            }*/

        }


        private static List<TcpConnection> OpenConnections = new List<TcpConnection>();
        private static Dictionary<TcpFlow, List<Entry>> TcpPackets = new Dictionary<TcpFlow, List<Entry>>();

        static void HandleTcpConnectionManagerOnConnectionFound(TcpConnection c)
        {
            OpenConnections.Add(c);
            c.OnConnectionClosed += HandleCOnConnectionClosed;
            c.Flows[0].OnPacketReceived += HandleOnPacketReceived;
            c.Flows[0].OnFlowClosed += HandleOnFlowClosed;
            c.Flows[1].OnPacketReceived += HandleOnPacketReceived;
            c.Flows[1].OnFlowClosed += HandleOnFlowClosed;
        }

        static void HandleCOnConnectionClosed(PosixTimeval timeval, TcpConnection connection, PacketDotNet.TcpPacket tcp, TcpConnection.CloseType closeType)
        {
            connection.OnConnectionClosed -= HandleCOnConnectionClosed;
            OpenConnections.Remove(connection);
        }

        static void HandleOnFlowClosed(PosixTimeval timeval, PacketDotNet.TcpPacket tcp, TcpConnection connection, TcpFlow flow)
        {
            // unhook the received handler
            flow.OnPacketReceived -= HandleOnPacketReceived;
        }

        private static object DictionaryLock = new object();
        static void HandleOnPacketReceived(PosixTimeval timeval, PacketDotNet.TcpPacket tcp, TcpConnection connection, TcpFlow flow)
        {
            lock (DictionaryLock)
            {
                if (!TcpPackets.ContainsKey(flow))
                {
                    TcpPackets[flow] = new List<Entry>();
                }

                var entry = new Entry(timeval, tcp);
                TcpPackets[flow].Add(entry);
            }
        }

        public class Entry
        {
            public SharpPcap.PosixTimeval Timeval;
            public PacketDotNet.Packet Packet;

            public Entry(SharpPcap.PosixTimeval timeval, PacketDotNet.Packet packet)
            {
                this.Timeval = timeval;
                this.Packet = packet;
            }
        }


        private List<MemoryStream> packetParserUsingDotnetConnections(String capFile)
        {
            //Open packet file
            //String capFile = "";
            ICaptureDevice device;

            try
            {
                // Get an offline device
                device = new CaptureFileReaderDevice(capFile);
                // tcpdump filter to capture only TCP/IP packets
                string filter = "ip and (tcp or udp)";
                //Convert to flows
                //string tcpFilter = "tcp.stream eq 1";
                //string udpFilter = "udp.stream==1";

                device.Filter = filter;

                // Open the device
                device.Open();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error - Caught exception when opening file" + e.ToString());
                return null;
            }




            RawCapture packets;
            List<byte> bytesList = new List<byte>();
            List<NetFlow> tcpNetFlows = new List<NetFlow>();
            List<NetFlow> udpNetFlows = new List<NetFlow>();
            TcpConnectionManager tcpConnectionManager = new TcpConnectionManager();
            tcpConnectionManager.OnConnectionFound += HandleTcpConnectionManagerOnConnectionFound;


            // Capture packets using GetNextPacket()
            while ((packets = device.GetNextPacket()) != null)
            {

                var p = PacketDotNet.Packet.ParsePacket(packets.LinkLayerType, packets.Data);
                var tcpPacket = p.Extract<PacketDotNet.TcpPacket>();
                var udpPacket = p.Extract<PacketDotNet.UdpPacket>();

                //tcpPacket, also requires assembling packets into proper sequences
                if (tcpPacket != null)
                {
                    tcpConnectionManager.ProcessPacket(packets.Timeval, tcpPacket);

                    var ipPacket = (PacketDotNet.IPPacket)tcpPacket.ParentPacket;
                    System.Net.IPAddress srcIp = ipPacket.SourceAddress;
                    System.Net.IPAddress dstIp = ipPacket.DestinationAddress;
                    int srcPort = tcpPacket.SourcePort;
                    int dstPort = tcpPacket.DestinationPort;


                }

                //Console.WriteLine(p.ToString());

            }

            // Print out the device statistics
            //Console.WriteLine(device.Statistics.ToString());

            //Close the pcap device
            device.Close();

            //MemoryStream memoryStream = new MemoryStream(bytesList.ToArray());
            // Dictionary<TcpFlow, List<Entry>> TcpPackets

            List<MemoryStream> memoryStreams = new List<MemoryStream>();
            MemoryStream memoryStream = null;

            foreach (KeyValuePair<TcpFlow, List<Entry>> entry in TcpPackets)
            {
                // do something with entry.Value or entry.Key
                foreach (var pkt in entry.Value)
                {
                    if (pkt.Packet.HasPayloadData)
                    {
                        memoryStream = new MemoryStream();
                        memoryStream.Write(pkt.Packet.PayloadData, 0, pkt.Packet.PayloadData.Length);
                        memoryStreams.Add(memoryStream);
                    }
                }
            }


            return memoryStreams;

        }

    }
}
