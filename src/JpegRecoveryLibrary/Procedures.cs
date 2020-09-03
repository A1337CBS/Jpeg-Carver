using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace JpegRecoveryLibrary
{
    public class Procedures
    {
        static PreCheck preCheck = new PreCheck();
        static Utils utils = new Utils();

        /*
         *Procedure 1- Recover single jpeg encoded data.
         * 
         */
        public Tuple<String, String> procedure_1(String rawPath)
        {
            String outFile = ""+rawPath+".jpg";
            Console.WriteLine("Running Procedure 1");
            if (!File.Exists(rawPath))
            {
                String msg = "Error - File does not exists.";
                return Tuple.Create(outFile, msg);
            }
            Procedures main = new Procedures();
            Huffman.switchHuffman(1);
            Stopwatch watch = Stopwatch.StartNew();
            //String rawPath = @"C:\Users\Ahmad\Desktop\QCRIInternship\Code\jpeg-carver-csharp-master\Dataset\Original\deagon_test_4kib";

            //Check if file is jpeg or not
            FileStream tempFileStream = new FileStream(rawPath, FileMode.Open);
            //Reading stream changes position, retain original
            long cpoint = tempFileStream.Position;
            bool isJpeg = preCheck.isJpeg(tempFileStream);
            tempFileStream.Position = cpoint;
            Console.WriteLine("Is Jpeg File?: " + isJpeg);

            //Check if jpeg partial headers exist
            utils.recoverHuffmanHdr(tempFileStream);

            tempFileStream.Close();


            Program p = new Program(rawPath);
            utils.recoverSegment(rawPath, p);

            //Console.WriteLine(" Hell Yeah!!");

            watch.Stop();
            Console.WriteLine(watch.Elapsed);
            //Console.ReadLine();
            return Tuple.Create(outFile, "Success");
        }

        ///* 
        // * Procedur 2- Recover whole storage volume.
        // * 
        // */
        public Tuple<String, String> procedure_2(String basePath)
        {
            String outFile = "";
            Console.WriteLine("Running Procedure 2");
            if (!File.Exists(basePath))
            {
                String msg = "Error - File does not exists.";
                return Tuple.Create(outFile, msg);
            }

            const int fileSystemBlockSize = 4 * 1024; //4kib
            FileStream fileStream = new FileStream(basePath, FileMode.Open);
            byte[] byteArray = new byte[fileSystemBlockSize];


            MemoryStream memStream = new MemoryStream();
            MemoryStream jpegFragMemStream = new MemoryStream();
            List<MemoryStream> memoryStreams = new List<MemoryStream>();
            //read chunk of bytes and check if it is jpeg
            while (fileStream.Read(byteArray, 0, fileSystemBlockSize) > 0)
            {
                memStream = new MemoryStream(byteArray);

                //Reading stream changes position, retain original
                long cpoint = memStream.Position;
                bool isJpeg = preCheck.isJpeg(memStream);
                memStream.Position = cpoint;

                if (isJpeg)
                {
                    jpegFragMemStream.Write(byteArray, 0, fileSystemBlockSize);
                }
                else
                {
                    if (jpegFragMemStream.Length > 0)
                    {
                        memoryStreams.Add(jpegFragMemStream);
                        Console.WriteLine("Jpeg Size: " + jpegFragMemStream.Length);
                        jpegFragMemStream = new MemoryStream();
                        //break;
                    }
                }

                Console.WriteLine("Is Jpeg File?: " + isJpeg);


            }
            memStream.Close();
            fileStream.Close();

            String filePath = Path.GetDirectoryName(basePath) + Path.DirectorySeparatorChar +"image_fragments";
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }

            for (int i = 0; i < memoryStreams.Count; i++)
            {
                String imageFragPath = filePath + Path.DirectorySeparatorChar+ "frag_" + i;
                Program p = new Program(imageFragPath, memoryStreams[i]);
                Console.WriteLine("Jpeg Size: " + memoryStreams[i].Length);

                utils.recoverHuffmanHdr(memoryStreams[i]);
                utils.recoverSegment(imageFragPath, p);
            }

            //Cleanup

            foreach (var stream in memoryStreams)
            {
                stream.Dispose();
            }
            memoryStreams.Clear();

            return Tuple.Create(outFile, "Success");
        }

        public Tuple<String, String> procedure_3(String packetPath)
        {
            String outFile = "";
            //Procedure 3: Apply Jpeg carver on Network Packets
            Console.WriteLine("Running Procedure 3");
            if (!File.Exists(packetPath))
            {
                String msg = "Error - File does not exists.";
                return Tuple.Create(outFile, msg);
            }
            Procedures main = new Procedures();

            List<MemoryStream> memoryStreams = utils.packetParser(packetPath);

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

                    //Reading stream changes position, retain original
                    long cpoint = memStream.Position;
                    bool isJpeg = preCheck.isJpeg(memStream);
                    memStream.Position = cpoint;


                    //Method to check and recover if jpeg partial headers exist
                    utils.recoverHuffmanHdr(memStream);

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
                Program p = new Program(imageFragPath, imgFragMemoryStreams[i]);
                Console.WriteLine("Jpeg Size: " + imgFragMemoryStreams[i].Length);
                utils.recoverSegment(imageFragPath, p);
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


            return Tuple.Create(outFile, "Success");
        }

        /*
         *Procedure 4 - Check if file fragment contains jpeg
         * 
         */
        public Tuple<String, String> procedure_4(String rawPath)
        {
            String outFile = "" + rawPath;
            Console.WriteLine("Running Procedure 4");
            if (!File.Exists(rawPath))
            {
                String msg = "Error - File does not exists.";
                return Tuple.Create(outFile, msg);
            }
            Stopwatch watch = Stopwatch.StartNew();
          
            //Check if file is jpeg or not
            FileStream tempFileStream = new FileStream(rawPath, FileMode.Open);

            bool isJpeg = preCheck.isJpeg(tempFileStream);

            tempFileStream.Close();

            if (isJpeg) {
                return Tuple.Create(outFile, "File fragment contains jpeg");
            }


            return Tuple.Create(outFile, "Does not seem to contain Jpeg");
        }


    }
}
