using System;
using System.IO;
using System.IO.Compression;
using System.Threading;


namespace ConsoleApp1
{
    class Decompress : ProcessData
    {
        public Decompress() {}

        public override bool Init(string source, string target)
        {
            if (!base.Init(source, target)) return false;
            if (!Validation(sourceStream)) return false;
            return true;
        }

        private bool Validation(FileStream sourceStream)
        {
            string[] line = sourceStream.Name.Split('.');
            if (line.Length != 2 || line[1] != "gz")
            {
                Console.WriteLine("The source file does not have the .gz extension.");
                if (!Program.Promt("Do you want continue anyway?")) return false;
            }

            byte[] valid = new byte[lenCRC];
            int bytesRead = ReadBlock(sourceStream, valid);
            if (!checkCRC(valid, 0))
            {
                Console.WriteLine("File is not valid archive. Abort.");
                return false;
            }
            return true;
        }

        public override void Read()
        {
            Console.WriteLine("Start Decompressing...");
            while (true)
            {

                byte[] buf = new byte[blockSize * 2];
                int bytesRead;

                bytesRead = ReadGzBlock(sourceStream, buf);

                if (bytesRead == 0) break;

                byte[] temp = new byte[bytesRead + lenCRC];
                Array.Copy(CRC, 0, temp, 0, 8);
                Array.Copy(buf, 0, temp, 8, bytesRead);

                int control_len = BitConverter.ToInt32(temp, temp.Length - 4);

                lock (locker1)
                {
                    block_compress_keys.Enqueue(new Block_byte() { block = temp, length = control_len, position = readBlocks });
                    Monitor.Pulse(locker1);
                }
                readBlocks++;

            }
            isRead = false;
            lock (locker1)
            {
                Monitor.PulseAll(locker1);
            }
        }

        public override void Work()
        {
            while (true)
            {
                Block_byte tmp = new Block_byte();
                tmp.block = new byte[blockSize];

                lock (locker1)
                {
                    while (block_compress_keys.Count == 0 && isRead)
                        Monitor.Wait(locker1);
                    if (block_compress_keys.Count == 0 && !isRead)
                        break;

                    tmp = block_compress_keys.Dequeue();
                }

                byte[] buf = new byte[tmp.length];

                using (MemoryStream source = new MemoryStream(tmp.block))
                {
                    using (GZipStream gzip = new GZipStream(source, CompressionMode.Decompress))
                    {
                        gzip.Read(buf, 0, buf.Length);

                        lock (locker2)
                        {
                            block_send_keys.Add(tmp.position, new Block_byte() { block = buf, length = buf.Length });
                            Monitor.Pulse(locker2);
                        }
                    }
                }
                long mem = GC.GetTotalMemory(true);
                if (mem > memoryPeak) memoryPeak = mem;
            }
            lock (locker2)
            {
                if (--workingThreads == 0)
                {
                    isWork = false;
                    Monitor.PulseAll(locker2);
                }
            }
        }
    }
}
