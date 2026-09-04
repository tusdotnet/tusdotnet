//using tusdotnet.benchmark.Benchmarks;

namespace tusdotnet.benchmark
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            // Uncomment the benchmark you want to run:

            // Full TusDiskStore comparison with PipeReader (most realistic)
            //var summary = BenchmarkRunner.Run<FlushStrategyBenchmark>();

            // Or run with command line args for more control:
            // dotnet run -c Release -- --filter *DirectFlush*
        }
    }
}
