using System;
using System.IO;
using System.Diagnostics;

namespace ConsoleApp1
{
    class Program
    {
        enum State
        {
            invalid = 0,
            compress = 1,
            decompress = 2
        }

        public static bool Promt(string question, string yesLabel = "yes", string noLabel = "no")
        {
            while (true)
            {
                Console.WriteLine(question + " " + yesLabel + '/' + noLabel);
                string line = Console.ReadLine().ToLower();

                if (line == yesLabel || line == yesLabel[0].ToString()) return true;
                if (line == noLabel || line == noLabel[0].ToString()) return false;
            }
        }

        static int Main(string[] args)
        {
            Stopwatch timer = new Stopwatch();
            ProcessData process;

            if (args.Length != 3 || (args[0].ToLower() != "compress" && args[0].ToLower() != "decompress"))
            {
                Console.WriteLine("Wrong type of compressing method. Please choose compress/decompress. Abort.");
                return 1;
            }

            string type = args[0];
            string sourceFile = args[1];
            string targetFile = args[2];

            State enumType = State.invalid;
            Enum.TryParse<State>(type, out enumType);

            switch (enumType)
            {
                case State.compress: process = new Compress(); break;
                case State.decompress: process = new Decompress(); break;
                default: return 1;
            }
            if (!process.Init(sourceFile, targetFile)) return 1;

            timer.Start();
            process.StartWorking();
            timer.Stop();

            Console.WriteLine("Memory peak : {0} megabytes", ProcessData.memoryPeak / (1024 * 1024));

            Console.WriteLine("Program execution time : {0:f} sec", (float)(timer.ElapsedMilliseconds) / 1000);
            GC.Collect();
            return 0;
        }
    }
}