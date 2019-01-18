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
            //var summary = BenchmarkRunner.Run<CreateAndWriteFile>();
            //var summary = BenchmarkRunner.Run<NoTusResumableHeader>();
            //var summary = BenchmarkRunner.Run<RequestIsNotForTusEndpoint>();

            //Test().GetAwaiter().GetResult();
        }

        private static async Task Test()
        {
            try
            {
                var benchmark = new CreateAndWriteFile();
                benchmark.GlobalSetup();
                await benchmark.CreateWriteDeleteFileIntentBased();
            }
            catch (Exception exc)
            {
                Console.Error.Write(exc.ToString());
            }

            Console.WriteLine("Press any key to exit");
            Console.ReadKey(true);
        }
    }
}
