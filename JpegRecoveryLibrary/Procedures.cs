using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace JpegRecoveryLibrary
{
    public class Procedures
    {
        static PreCheck preCheck = new PreCheck();

        /*
         *Procedure 1- Recover single jpeg encoded data.
         * 
         */
        public void procedure_1(String rawPath) {
            Console.WriteLine("Running Procedure 1");
            Procedures main = new Procedures();
            Huffman.switchHuffman(1);
            Stopwatch watch = Stopwatch.StartNew();
            //String rawPath = @"C:\Users\Ahmad\Desktop\QCRIInternship\Code\jpeg-carver-csharp-master\Dataset\Original\deagon_test_4kib";

            //Check if file is jpeg or not
            FileStream testFileStream = new FileStream(rawPath, FileMode.Open);
            bool isJpeg = preCheck.isJpeg(testFileStream);
            Console.WriteLine("Is Jpeg File?: " + isJpeg);

            //Check if jpeg partial headers exist
            //Find SOS marker
            var sos = preCheck.get_sos_cnt_and_point(testFileStream);
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
            // Set the stream position to the beginning of the file.
            testFileStream.Seek(0, SeekOrigin.Begin);

            /*DHT
             * FF C4 - Marker(2 bytes)
             * XX XX - Length(2 bytes)
             * XX    - Table info(1 byte) Upper nibble = 0/1 (DC=0/AC=1), Lower nibble = 0-3 Table ID
             * [16]  - # of codes of each length
             * [X]   - Symbols
             */

            var dht = preCheck.get_dht_point(testFileStream);
            int dht_index = dht.Item1; // DHT is hit or not
            long dht_point = dht.Item2; // point of DHT data starts.
            if (dht_index == -1)
            {
                Console.WriteLine("DHT marker not found");
            }
            else
            {
                Console.WriteLine("DHT marker starts at:" + dht_point);
            }

            testFileStream.Seek(0, SeekOrigin.Begin);
            preCheck.parseDht(testFileStream, dht_point);

            testFileStream.Close();


            Program p = new Program(rawPath);
            main.recoverSegment(rawPath, p);

            Console.WriteLine(" Hell Yeah!!");

            watch.Stop();
            Console.WriteLine(watch.Elapsed);
            //Console.ReadLine();

        }

        public void procedure_2()
        {
        }

            public void procedure_3() {
            /*
            Console.WriteLine("Running Procedure 3");
            Procedures main = new Procedures();
            String packetPath = @"G:\HBKU\QCRIInternship\pcap.pcap";
            String packetPathTemp = "raw_temp";
            List<MemoryStream> memoryStreams = m.packetParser(packetPath);

            const int fileSystemBlockSize = 4 * 1024; //4kib
            byte[] byteArray = new byte[fileSystemBlockSize];
            MemoryStream memStream = new MemoryStream();
            MemoryStream jpegFragMemStream = new MemoryStream();
            List<MemoryStream> imgFragMemoryStreams = new List<MemoryStream>();

            for (int i = 0; i < memoryStreams.Count; i++)
            {
                memoryStreams[i].Position = 0;
                while (memoryStreams[i].Read(byteArray, 0, fileSystemBlockSize) > 0)
                {
                    memStream = new MemoryStream(byteArray);
                    bool isJpeg = preCheck.isJpeg(memStream);

                    if (isJpeg)
                    {
                        jpegFragMemStream.Write(byteArray, 0, fileSystemBlockSize);
                    }
                    else
                    {
                        if (jpegFragMemStream.Length > 0)
                        {
                            imgFragMemoryStreams.Add(jpegFragMemStream);
                            Console.WriteLine("Jpeg Size: " + jpegFragMemStream.Length);
                            jpegFragMemStream = new MemoryStream();
                            //break;
                        }
                    }

                    Console.WriteLine("Is Jpeg File?: " + isJpeg);

                }
            }

            String packetfragPath = "G:\\HBKU\\QCRIInternship\\pkt_image_fragments";
            if (!Directory.Exists(packetfragPath))
            {
                Directory.CreateDirectory(packetfragPath);
            }

            for (int i = 0; i < imgFragMemoryStreams.Count; i++)
            {
                bool isJpeg = preCheck.isJpeg(imgFragMemoryStreams[i]);
                Console.WriteLine("Is pkt Jpeg File?: " + isJpeg);
                String imageFragPath = packetfragPath + "\\frag_" + i;
                p = new Program(imageFragPath, imgFragMemoryStreams.ElementAt(i));
                Console.WriteLine("Jpeg Size: " + imgFragMemoryStreams.ElementAt(i).Length);
                main.recoverSegment(imageFragPath, p);
            }
            //Cleanup

            foreach (var stream in memoryStreams)
            {
                stream.Dispose();
            }
            memoryStreams.Clear();

            foreach (var stream in imgFragMemoryStreams)
            {
                stream.Dispose();
            }
            imgFragMemoryStreams.Clear();
            */
        }

        long recoverSegment(string file, Program p)
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

    }
}
