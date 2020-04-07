using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;

namespace ConsoleApp1
{
    public struct Block_byte
    {
        public byte[] block;
        public int length;
        public int position;
    }
    abstract class ProcessData : IDisposable
    {
        protected Queue<Block_byte> block_compress_keys = new Queue<Block_byte>();
        protected Dictionary<int, Block_byte> block_send_keys = new Dictionary<int, Block_byte>();

        public const int lenCRC = 8;
        protected static byte[] CRC = new byte[lenCRC] { 0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00 };

        public const int blockSize = 1024 * 1024;
        public static long memoryPeak = 0;

        protected int readBlocks = 0;
        protected int sendBlocks = 0;
        protected int workingThreads = 0;

        protected int processorCount;

        protected bool isRead = true;
        protected bool isWork = true;

        protected static FileStream sourceStream;
        protected static FileStream targetStream;

        protected object locker1 = new object();
        protected object locker2 = new object();

        static Thread[] myThreads;

        public ProcessData()
        {
            processorCount = Environment.ProcessorCount;
            workingThreads = processorCount - 2;
        }

        public virtual bool Init(string source, string target)
        {
            if (!File.Exists(source))
            {
                Console.WriteLine("File {0} does not exist.\n Abort process.", source);
                return false;
            }
            if (File.Exists(target))
            {
                Console.WriteLine("File {0} is already exist.", target);

                if (Program.Promt("Do you want to rewrite it?")) File.Delete(target);
                else return false;
            }
            sourceStream = File.OpenRead(source);
            targetStream = File.OpenWrite(target);
            return true;
        }

        public abstract void Read();

        public abstract void Work();

        public void StartWorking()
        {
            //processorCount = 3;
            processorCount = Environment.ProcessorCount;
            Console.WriteLine("Number of treads created: {0}", processorCount);

            myThreads = new Thread[processorCount];

            myThreads[0] = new Thread(Read);
            myThreads[0].Start();

            for (int i = 1; i < processorCount - 1; i++)
            {
                myThreads[i] = new Thread(Work);
                myThreads[i].Start();
            }

            myThreads[processorCount - 1] = new Thread(Send);
            myThreads[processorCount - 1].Start();

            for (int i = 0; i < processorCount; i++)
            {
                myThreads[i].Join();
            }
            Console.WriteLine("Done!\nFirst file size : {0} bytes.\nSecond file size : {1} bytes.", sourceStream.Length, targetStream.Length);
        }

        public int ReadBlock(Stream stream, byte[] block)
        {
            int position = 0;
            while (position < block.Length)
            {
                var actuallyRead = stream.Read(block, position, block.Length - position);
                if (actuallyRead == 0) break;
                position += actuallyRead;
            }
            return position;
        }

        public int ReadGzBlock(Stream stream, byte[] block)
        {
            int position = 0;
            while (position < block.Length)
            {
                int temp = stream.ReadByte();
                if (temp == -1) break;

                block[position++] = (byte)temp;

                if (position > lenCRC && checkCRC(block, position - lenCRC))
                {
                    position -= lenCRC;
                    break;
                }
            }
            return position;
        }
        public static bool checkCRC(byte[] block, int position)
        {
            for (int i = 0; i < lenCRC; i++)
            {
                if (CRC[i] != block[position + i]) return false;
            }
            return true;
        }

        public void Send()
        {
            byte[] temp;
            int length;

            while (true)
            {
                lock (locker2)
                {
                    while (!block_send_keys.ContainsKey(sendBlocks) && isWork)
                        Monitor.Wait(locker2);
                    if (block_send_keys.Count == 0 && !isWork)
                        break;

                    length = block_send_keys[sendBlocks].length;
                    temp = block_send_keys[sendBlocks].block;

                    block_send_keys.Remove(sendBlocks++);
                }
                targetStream.Write(temp, 0, length);
            }
        }
        public void Dispose()
        {
            sourceStream.Close();
            targetStream.Close();
        }
    }
}
