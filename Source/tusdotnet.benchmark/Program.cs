using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;
using tusdotnet.benchmark.Benchmarks;

namespace tusdotnet.benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<CreateWriteDeleteFile>();

            //Test().GetAwaiter().GetResult();
        }

        private static async Task Test()
        {
            await new CreateWriteDeleteFile().CreateIntentBased();
            Console.ReadKey(true);
        }
    }
}
