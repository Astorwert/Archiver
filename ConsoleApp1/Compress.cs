using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace ConsoleApp1
{
    internal class Compress : ProcessData
    {
        public Compress() { }

        public override bool Init(string source, string target) 
        { 
            return base.Init(source, target); 
        }

        public override void Read()
        {
            Console.WriteLine("Start Compressing...");
            while (true)
            {
                int count;
                lock (locker1)
                {
                    count = block_compress_keys.Count;
                }
                if (count < processorCount - 2)
                {
                    byte[] buf = new byte[blockSize];

                    var bytesRead = ReadBlock(sourceStream, buf);
                    if (bytesRead == 0) break;

                    lock (locker1)
                    {
                        block_compress_keys.Enqueue(new Block_byte() { block = buf, length = bytesRead, position = readBlocks });
                        Monitor.Pulse(locker1);
                    }
                    readBlocks++;
                }
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
                int count;
                lock (locker1)
                {
                    count = block_send_keys.Count;
                }
                if (count <= processorCount)
                {
                    using (MemoryStream memory = new MemoryStream())
                    {
                        Block_byte tmp = new Block_byte();
                        using (GZipStream gzip = new GZipStream(memory, CompressionMode.Compress))
                        {
                            tmp.block = new byte[blockSize];

                            lock (locker1)
                            {
                                while (block_compress_keys.Count == 0 && isRead)
                                    Monitor.Wait(locker1);
                                if (block_compress_keys.Count == 0 && !isRead)
                                    break;

                                tmp = block_compress_keys.Dequeue();
                            }

                            gzip.Write(tmp.block, 0, tmp.length);
                        }

                        byte[] buf = memory.ToArray();

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
