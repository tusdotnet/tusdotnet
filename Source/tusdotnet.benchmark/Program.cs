using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;
using tusdotnet.benchmark.Benchmarks;

namespace tusdotnet.benchmark
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            // Uncomment the benchmark you want to run:

            // Direct comparison of flush strategies (recommended - fastest to run)
            var summary = BenchmarkRunner.Run<FlushStrategyBenchmark>();

            // Full TusDiskStore comparison with PipeReader (most realistic)
            //var summary = BenchmarkRunner.Run<FlushStrategyBenchmark>();

            // Or run with command line args for more control:
            // dotnet run -c Release -- --filter *DirectFlush*
        }
    }
}
