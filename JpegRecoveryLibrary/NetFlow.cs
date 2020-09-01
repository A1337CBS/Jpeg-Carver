using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace JpegRecoveryLibrary
{
    public class NetFlow
    {
        //NetFlow consists of atleast 4 tuples
        private System.Net.IPAddress srcIp;
        private System.Net.IPAddress dstIp;
        private int srcPort;
        private int dstPort;
        //List<MemoryStream> payloadData;
        //List<int> seqNum;
        Dictionary<uint, byte[]> payloadData;


        NetFlow(){}

        public NetFlow(IPAddress srcIp, IPAddress dstIp, int srcPort, int dstPort)
        {
            this.srcIp = srcIp;
            this.dstIp = dstIp;
            this.srcPort = srcPort;
            this.dstPort = dstPort;
            this.payloadData = new Dictionary<uint, byte[]>();
           // this.seqNum = new List<int>();
        }

        public int payloadDataCount() {
            return payloadData.Count;
        }

        public void addPacketData(uint seqNum, byte[] payloadData) {
            //this.seqNum.Add(seqNum);
            //this.payloadData.Add(new MemoryStream(payloadData));
            this.payloadData.Add(seqNum, payloadData);
        }

        public bool isPresentSeqNum(uint key) {
            return this.payloadData.ContainsKey(key);
        }

        //returns flow Data in order
        public MemoryStream getAssembledFlowData() {
            MemoryStream memoryStream = null;
            if (this.payloadData.Count > 0)
            {
                memoryStream = new MemoryStream();
                // Acquire keys and sort them.
                var keys_sorted = this.payloadData.Keys.ToList();
                keys_sorted.Sort();
                foreach (var k in keys_sorted)
                {
                    Console.WriteLine("SeqNum {0} = {1}", k, this.payloadData[k]);
                    memoryStream.Write(this.payloadData[k], 0, this.payloadData[k].Length);
                }
            }
            return memoryStream;
        }

        public override bool Equals(object obj)
        {
            return obj is NetFlow flow &&
                   EqualityComparer<IPAddress>.Default.Equals(srcIp, flow.srcIp) &&
                   EqualityComparer<IPAddress>.Default.Equals(dstIp, flow.dstIp) &&
                   srcPort == flow.srcPort &&
                   dstPort == flow.dstPort;
        }
    }
}
