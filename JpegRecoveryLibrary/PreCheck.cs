using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using static JpegRecoveryLibrary.Huffman;

namespace JpegRecoveryLibrary
{
    public class PreCheck
    {
        int chunk_size = 4 * 1024;
        //Stream fileStream argument mainly supports FileStream, MemoryStream objects 
        public bool isJpeg(Stream fileStream)
        {
            //1)If the number of remaining byte boundaries is equal tozero, it is accepted as non - JPEG encoded data.
            //2)If the number of remaining byte boundaries is equal toexactly one, it is accepted as JPEG encoded data.
            //3)If the  number of  remaining  byte boundaries  is biggerthan  one,  then,  check through  the Oscar  method toaccept or reject the encoded data
            long cpoint = fileStream.Position;
            int cnt_rem_bnd = get_cnt_rem_bnd(fileStream);
            //int cnt_rem_bnd = 2;// only run oscar method

            if (cnt_rem_bnd == 0)
            {
                // non-jpeg
                return false;
            }
            else if (cnt_rem_bnd == 1)
            {
                //jpeg
                return true;
            }
            else
            {
                // follow oscar method
                fileStream.Position = cpoint;
                int[] ff00_ffda_brst_crst = search_ff00_ffda_rst(fileStream);
                bool flag_ff00 = (ff00_ffda_brst_crst[0] > 9.7) && (ff00_ffda_brst_crst[0] < 47);
                bool flag_rst = (ff00_ffda_brst_crst[2] == 0) || (ff00_ffda_brst_crst[3] == 1);

                //flag_rst
                //a--> ff00_ffda_brst_crst[2] --> has rst marker
                //b--> ff00_ffda_brst_crst[3] --> is in mod 8
                //-----------------------------------------------
                //a   b|flag_rst
                //0   0|1
                //0   1|1
                //1   0|0
                //1   1|1
                //flag_rst=( not a) or b

                return flag_ff00 && flag_rst;
            }
        }

        int get_cnt_rem_bnd(Stream fileStream)
        {
            //chunk_size=8,16,32 KB at most
            int cnt_rem_bnd = 8;
            //fileStream.Position = position;
            int[] cand = { 1, 1, 1, 1, 1, 1, 1, 1 };
            uint bound_value = (uint)((((fileStream.ReadByte() << 8) + fileStream.ReadByte()) << 8) + fileStream.ReadByte());
            uint test_value;
            for (int i = 0; i < chunk_size; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    test_value = (bound_value << (j + 8)) >> 16;
                    if ((65280 < test_value && test_value < 65488) || (65497 < test_value && test_value <= 65535))// FF01<=test_value<=FFCF or FFDA<=test_value<=FFFF
                    {
                        cand[j] = 0;
                        if (cand.Sum() == 0)
                        {
                            cnt_rem_bnd = 0;
                            return cnt_rem_bnd;
                        }
                    }
                }
                bound_value = (uint)(((bound_value << 16) >> 8) + fileStream.ReadByte());
            }

            cnt_rem_bnd = cand.Sum();
            return cnt_rem_bnd;
        }

        #region Dht

        private struct Huff
        {
            //public uint numSymbols;
            public int[][] huffTable;
            public bool set;
            public int ACTable; // 0=false, 1= true
            public int tableID;
            public int[] maxcode;
            public int[] mincode;
            public int[] valptr;
            public List<byte> huffval;
            public int[] BITS;
            public int[] huffSize;
            public uint[] HUFFCODE;
            public int maxcodelength;
        }

        private Huffman.DHTStruct Huff2DHTStruct(Huff[] HuffInput)
        {
            Huffman.DHTStruct dht = new Huffman.DHTStruct();
            dht.mincode = new int[4][];
            dht.maxcode = new int[4][];
            dht.valptr = new int[4][];
            dht.huffval = new byte[4][];
            dht.maxcodelength = new int[4];

            // Append to last line
            for (int i = 0; i < HuffInput.Length; i++)
            {
                dht.mincode[i] = HuffInput[i].mincode;
                dht.maxcode[i] = HuffInput[i].maxcode;
                dht.valptr[i] = HuffInput[i].valptr;
                Console.WriteLine("Huff i" + i);
                dht.huffval[i] = HuffInput[i].huffval.ToArray();
                dht.maxcodelength[i] = HuffInput[i].maxcodelength;
            }

            return dht;
        }

        // reverse byte order (16-bit)
        private UInt16 ReverseBytes(UInt16 value)
        {
            return (UInt16)((value & 0xFFU) << 8 | (value & 0xFF00U) >> 8);
        }

        public void parseDht(Stream fileStream, long dht_point = 0)
        {

            // Set the stream position to after dht marker.
            fileStream.Seek(dht_point + 2, SeekOrigin.Begin);

            Console.WriteLine("Reading DHT marker");
            BinaryReader binReader = new BinaryReader(fileStream);
            //binReader.BaseStream.Position = dht_point*8;
            //read length in BigEndian
            int length = (int)ReverseBytes(binReader.ReadUInt16());
            length -= 2;
            Console.WriteLine("DHT Length (bytes):" + length);
           // Console.WriteLine("fileStream Length:" + fileStream.Length);
            Huff[] _dht = new Huff[4];
            int index = 0;


            while (length > 0)
            {

                _dht[index].huffTable = new int[16][];
                _dht[index].huffval = new List<byte>();
                _dht[index].maxcode = new int[17];
                _dht[index].mincode = new int[17];
                _dht[index].valptr = new int[16];
                _dht[index].BITS = new int[17];
                _dht[index].huffSize = new int[163];
                _dht[index].HUFFCODE = new uint[163];

                byte tableInfo = binReader.ReadByte();

                int tableID = tableInfo & 0x0F; ;
                _dht[index].tableID = tableID;

                int ACTable = tableInfo >> 4;
                _dht[index].ACTable = ACTable;
                Console.WriteLine("tableID:" + tableID);
                Console.WriteLine("ACTable:" + ACTable);

                if (tableID > 3)
                {
                    Console.WriteLine("Error - Invalid Huffman Table ID: " + tableID);
                    return;
                }

                uint allSymbols = 0;
                uint numSymbols = 0;
                //Read numOfSymbols from DHT header
                for (uint i = 0; i < 16; i++)
                {
                    numSymbols = binReader.ReadByte();
                    _dht[index].BITS[i + 1] = (int)numSymbols;
                    allSymbols += numSymbols;
                    _dht[index].huffTable[i] = new int[numSymbols];
                    //  _dht[index].numSymbols = numSymbols;

                }

                if (allSymbols > 162)
                {
                    Console.WriteLine("Error - Too many symbols in Huffman table " + allSymbols);
                    return;
                }

                for (int i = 0; i < 16; i++)
                {
                    if (_dht[index].huffTable[i].Length == 0)
                    {
                        continue;
                    }
                    for (int j = 0; j < _dht[index].huffTable[i].Length; j++)
                    {
                        sbyte s = unchecked((sbyte)binReader.ReadByte());
                        _dht[index].huffTable[i][j] = s;
                        _dht[index].huffval.Add((byte)s);
                    }
                }

                for (int i = 0; i < 16; i++)
                {
                    Console.WriteLine("Huffman table index " + i + ": " + "[{0}]", string.Join(", ", _dht[index].huffTable[i]));
                }

                Console.WriteLine("Huffman table  " + string.Join(", ", _dht[index].huffval));

                //Generate_size_table(huff_size)
                int K = 0, I = 1, J = 1;
                while (true)
                {
                    if (J > _dht[index].BITS[I])
                    {
                        I = I + 1;
                        J = 1;
                    }
                    else
                    {
                        _dht[index].huffSize[K] = I;
                        K = K + 1;
                        J = J + 1;
                        continue;
                    }

                    if (I > 16)
                    {
                        _dht[index].huffSize[K] = 0;
                        break;
                    }

                }

                //for (int k = 0; k < _dht[index].huffSize.Length; k++)
                //{
                //    Console.WriteLine("HuffmanSize table " + _dht[index].huffSize[k]);
                //}

                //Generate_code_table
                K = 0;
                uint CODE = 0b_0;
                int SI = _dht[index].huffSize[0];
                while (true)
                {
                    _dht[index].HUFFCODE[K] = CODE;
                    CODE = CODE + 1;
                    K = K + 1;
                    if (_dht[index].huffSize[K] == SI)
                    {
                        continue;
                    }

                    if (_dht[index].huffSize[K] == 0)
                    {
                        break;
                    }
                    else
                    {
                        CODE = CODE << 1;
                        SI = SI + 1;
                        while (_dht[index].huffSize[K] != SI)
                        {
                            CODE = CODE << 1;
                            SI = SI + 1;
                        }
                    }


                }

                //for (int k = 0; k < _dht[index].HUFFCODE.Length; k++)
                //{
                //    Console.WriteLine("HuffmanCode table " + Convert.ToString(_dht[index].HUFFCODE[k], toBase: 2) );
                //}

                //Decoder_tables
                I = 0;
                J = 0;
                while (true)
                {
                    I = I + 1;
                    if (I > 16)
                    {
                        break;
                    }
                    if (_dht[index].BITS[I] == 0)
                    {
                        _dht[index].maxcode[I] = -1;
                    }
                    else
                    {
                        _dht[index].valptr[I - 1] = J;
                        _dht[index].mincode[I] = (int)_dht[index].HUFFCODE[J];
                        J = J + _dht[index].BITS[I] - 1;
                        _dht[index].maxcode[I] = (int)_dht[index].HUFFCODE[J];
                        J = J + 1;
                    }

                }

                /*for (int k = 1; k < _dht[index].maxcode.Length; k++)
                //{
                //    Console.WriteLine("HuffmanMaxCode table " + _dht[index].maxcode[k]);
                //}

                for (int k = 1; k < _dht[index].mincode.Length; k++)
                {
                    Console.WriteLine("HuffmanMInCode table " + _dht[index].mincode[k]);
                }*/

                for (int k = 0; k < _dht[index].valptr.Length; k++)
                {
                    Console.WriteLine("Huffman valptr table " + _dht[index].valptr[k]);
                }

                _dht[index].maxcodelength = (int)(Math.Log(_dht[index].maxcode.Max(), 2)) + 1;

                Console.WriteLine("MaxCodeLength " + _dht[index].maxcodelength);

                length = length - (int)allSymbols - 17;
                index++;
            }

            //Write everything to file
            String path = "Huffman.json";

            //Check if atleast four Huffman tables were present, otherwise invalid --> Change this limitation in the future
            if (index < 4) {
                return;
            }

            //Convert to Huffman.DHTStruct format
            DHTStruct DHTrecord = Huff2DHTStruct(_dht);
            DHTrecord.id = DHTrecord.GetHashCode();
            DHTrecord.count = 0;
            //List<DHTStruct> DHTsList = new List<DHTStruct>();
            List<DHTStruct> DHTsList = null;
            //DHTsList.Add(DHTrecord);

            //// See if file exists,            
            if (File.Exists(path))
            {
                try
                {
                    
                    /// Read in stream and check if DHT is already present 
                    using (StreamReader r = new StreamReader(path))
                    {
                        String json = r.ReadToEnd();
                        DHTsList = JsonConvert.DeserializeObject<List<DHTStruct>>(json);
                        bool recordFound = false;
                        //Check if DHTrecord already present in list
                        for(int i=0; i< DHTsList.Count;i++){
                            if (DHTsList[i].id == DHTrecord.id) {
                                DHTrecord.count = DHTsList[i].count + 1;
                                DHTsList[i] = DHTrecord;
                                recordFound = true;
                            }
                        }

                        // Console.WriteLine("jarray:  " + jarray[0].huffval);
                        if (!recordFound && DHTsList!=null) {
                            DHTsList.Add(DHTrecord);
                        }

                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine("Error - " + e.Message);
                    return;
                }
            }

            // Save DHT to Huffman.json
            if (DHTsList == null)
            {
                DHTsList = new List<DHTStruct>();
                DHTsList.Add(DHTrecord);
            }

            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;
            //Create a new file every time
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            fs.Close();
            using (StreamWriter sw = File.AppendText(path))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, DHTsList.ToArray());
            }

            

        }

        #endregion

        int[] search_ff00_ffda_rst(Stream fileStream)
        {

            // cnt_ff00:cnt_ffda:rst_in_8_loop

            //chunk_size=4 KB at most
            int cnt_ff00 = 0;// 1 - ff00
            int cnt_ffda = 0;// 2 - ffda
            int has_rst_marker = 0;// 3- rst marker flag
            int is_rst_in_mod8 = 0;// 4- rst modulus==8 flag

            bool check_rst = true;
            bool flag_loop_all_rst_markers = true;
            int next_rst_marker = 0;
            int rst_marker_offset = 0;

            //fileStream.Position = position;
            uint bound_value = readNexNBytes(fileStream, 3);
            uint test_value;
            for (int i = 0; i < chunk_size; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    test_value = (bound_value << (j + 8)) >> 16;
                    if (65280 == test_value)//test_value=FF00
                    {
                        cnt_ff00++;
                    }
                    else if (65498 == test_value)//test_value=FFDA
                    {
                        cnt_ffda++;
                    }

                    if (check_rst)
                    {
                        if (flag_loop_all_rst_markers)
                        {
                            for (int k = 0; k < 8; k++)
                            {
                                if ((65488 + k) == test_value)//test_value=FF00
                                {
                                    flag_loop_all_rst_markers = false;
                                    next_rst_marker = (k < 7) ? 65488 + k + 1 : 65488;
                                    rst_marker_offset = j;
                                    has_rst_marker = 1;
                                    is_rst_in_mod8 = 1;
                                }
                            }
                        }
                        else
                        {
                            if (next_rst_marker == test_value)
                            {
                                if (rst_marker_offset == j)
                                {
                                    // continue
                                    next_rst_marker = (next_rst_marker == 65495) ? 65488 : next_rst_marker + 1;
                                }
                                else
                                {
                                    // rst marker modulus is not 8; break not a jpeg encoded data (for RST marker included ones)
                                    check_rst = false;
                                    is_rst_in_mod8 = 0;
                                }
                            }
                        }
                    }


                }
                bound_value = (uint)(((bound_value << 16) >> 8) + fileStream.ReadByte());
            }
            //return cnt_ff00 + ":" + cnt_ffda + ":" + has_rst_marker + ":" + is_rst_in_mod8;
            return new int[] { cnt_ff00, cnt_ffda, has_rst_marker, is_rst_in_mod8 };
        }


        public Tuple<int, long> get_sos_cnt_and_point(Stream fileStream, bool isChunk = false)
        {
            //0xFFDA00 08010100 003F00
            //0xFFDA00 0C030100 02110311 003F00
            //0xFFDA00 0C030000 01110211 003F00
            //0xFFDA00 0C030000 01000200 003F00
            //0xFFDA00 0C035200 47004200 003F00
            long last_point = (isChunk) ? fileStream.Position + 4 * 1024 : fileStream.Length;

            string[] sos2nd = { "0x08010100", "0x0C030100", "0x0C030000", "0x0C035200" };
            string[] sos3rd = { "0x02110311", "0x01110211", "0x01000200", "0x47004200" };
            int[] cnt_sos = new int[5];// keeps number of sos markers in above order
            long sos_point = 0; // holds latest sos_point
            uint bit_buffer = readNexNBytes(fileStream, 4);
            int offset;
            uint remainder = 0;
            uint sos_buffer = 0;

            int sos_i = -1, sos_fin;

            while (fileStream.Position < last_point)
            {
                offset = searchHex(bit_buffer, "0xFFDA00");
                remainder = getRemainder(bit_buffer, offset);
                if (offset < 8)
                {
                    // hit
                    bit_buffer = readNexNBytes(fileStream, 4);
                    sos_buffer = join2Remainder(remainder, bit_buffer, offset);
                    remainder = getRemainder(bit_buffer, offset);

                    // search 2nd sos blocks
                    sos_i = searchSosBlock(sos_buffer, sos2nd);

                    if (sos_i < 4)
                    {
                        //hit
                        if (sos_i > 0)
                        {
                            //check for 3rd blocks in sos-2/3/4/5
                            bit_buffer = readNexNBytes(fileStream, 4);
                            sos_buffer = join2Remainder(remainder, bit_buffer, offset);
                            remainder = getRemainder(bit_buffer, offset);
                            sos_i = searchSosBlock(sos_buffer, sos3rd) + 1;
                        }

                        if (sos_i < 5)
                        {
                            // check sos_fin
                            bit_buffer = readNexNBytes(fileStream, 4);
                            sos_buffer = join2Remainder(remainder, bit_buffer, offset);
                            remainder = getRemainder(bit_buffer, offset);
                            sos_fin = searchHex(sos_buffer, "0x003F00");
                            if (sos_fin < 8)
                            {
                                //match!
                                cnt_sos[sos_i] += 1;
                                sos_point = fileStream.Position + ((sos_i == 0) ? 11 : 15);
                                break;
                            }
                        }
                    }
                }
                bit_buffer = (uint)(((bit_buffer << 8) + fileStream.ReadByte()));
            }
            // if there is no hit returns (-1,0)
            return Tuple.Create(sos_i, sos_point);
        }

        public static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        //find dht marker before SOS point 
        // public int get_dht_point(FileStream fileStream, int sos_point)
        public Tuple<int, long> get_dht_point(Stream fileStream)
        {

            // uint bit_buffer = readNexNBytes(fileStream, 4);
            int offset;
            uint remainder = 0;
            uint sos_buffer = 0;

            int dht_i = -1;
            long dht_point = 0;

            string match = "FFC4";
            byte[] matchBytes = StringToByteArray(match);
            int i = 0;
            int readByte;

            // while ( (readByte = fileStream.ReadByte()) != -1 )//&& fileStream.Position < sos_point)
            while (fileStream.Position < fileStream.Length)
            {
                /*
                offset = searchHex(bit_buffer, "0xFFC4");
                Console.WriteLine("It" + bit_buffer);
                if (offset < 8)
                {
                    dht_i = 0;
                    Console.WriteLine("It found between {0} and {1}.",
                           fileStream.Position, fileStream.Position+offset);
                    dht_point = fileStream.Position + offset;
                    break;
                }
                bit_buffer = readNexNBytes(fileStream, 4);
                */
                //bit_buffer = (uint)(((bit_buffer << 8) + fileStream.ReadByte()));
                readByte = fileStream.ReadByte();
                if (matchBytes[i] == readByte)
                {
                    i++;
                }
                else
                {
                    i = 0;
                }
                if (i == matchBytes.Length)
                {
                    dht_i = 1;
                    Console.WriteLine("It found between {0} and {1}.",
                           fileStream.Position - matchBytes.Length, fileStream.Position);
                    dht_point = fileStream.Position - matchBytes.Length;
                    break;
                }
            }


            // if there is no hit returns (-1,0)
            return Tuple.Create(dht_i, dht_point);
        }

        uint readNexNBytes(Stream fileStream, int n)
        {
            // reads maximum next n bytes
            uint nextNBytes = 0;
            for (int i = 0; i < n; i++)
            {
                nextNBytes = (uint)((nextNBytes << 8) + fileStream.ReadByte());
            }

            return nextNBytes;
        }

        int searchHex(uint bit_buffer, string prefixedHex)
        {
            uint test_value;
            int j;
            for (j = 0; j < 8; j++)
            {
                test_value = (bit_buffer << j) >> 8;
                if (hex2int(prefixedHex) == test_value)//test_value=prefixedHex
                {
                    return j;
                }
            }
            return j;
        }

        int hex2int(string prefixedHex)
        {
            return Convert.ToInt32(prefixedHex, 16);
        }

        uint getRemainder(uint bit_buffer, int j)
        {
            // get remainder bits shifted to lhs
            return (bit_buffer << (j + 24));
        }

        uint join2Remainder(uint remainder, uint bit_buffer, int j)
        {
            return remainder + ((bit_buffer << j) >> (8 - j));
        }

        int searchSosBlock(uint sos_buffer, string[] sos_block)
        {
            int k;
            for (k = 0; k < sos_block.Length; k++)
            {
                if (sos_buffer == (uint)hex2int(sos_block[k]))
                {
                    break;
                }
            }

            return k;
        }
    }
}
