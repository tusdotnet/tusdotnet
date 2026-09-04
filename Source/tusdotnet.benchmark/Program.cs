using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;
using tusdotnet.benchmark.Benchmarks;

namespace tusdotnet.benchmark
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            // Usage:
            //   dotnet run -c Release -- alloc          → BenchmarkDotNet PipeReader allocation benchmark
            //   dotnet run -c Release -- stream         → BenchmarkDotNet Stream allocation benchmark
            //   dotnet run -c Release -- buffering       → wall-clock buffering benchmark (1s/chunk)
            //   dotnet run -c Release -- buffering quick → buffering benchmark (10ms/chunk, fast)

            var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "alloc";

            if (mode == "alloc")
            {
                BenchmarkRunner.Run<ReadAsyncAllocationBenchmark>();
                return;
            }

            if (mode == "stream")
            {
                BenchmarkRunner.Run<StreamReadAsyncAllocationBenchmark>();
                return;
            }

            // ── legacy buffering benchmark ───────────────────────────────────────
            var quick = args.Length > 1 && args[1] == "quick";
            var bench = new ClientDisconnectBufferingBenchmark(chunkDelayMs: quick ? 10 : 1000);
            bench.Setup();

            try
            {
                const int runs = 3;
                var delayMode = quick ? "quick (10ms/chunk)" : "1s/chunk (~49 KB/s)";
                Console.WriteLine(
                    $"Running {runs} runs each. File: 10 MB, chunk: 49 KB, delay: {delayMode}."
                );
                Console.WriteLine();

                long oldTotal = 0,
                    newTotal = 0;

                for (int i = 1; i <= runs; i++)
                {
                    Console.WriteLine($"--- Run {i}/{runs} ---");

                    Console.Write("[OLD] ");
                    var sw = Stopwatch.StartNew();
                    var (oldReqs, _) = await bench.UploadWithOldReaderDebug();
                    sw.Stop();
                    oldTotal += sw.ElapsedMilliseconds;
                    Console.WriteLine($"{sw.Elapsed:mm\\:ss\\.ff}  ({oldReqs} requests)");

                    Console.Write("[NEW] ");
                    sw.Restart();
                    var (newReqs, _) = await bench.UploadWithNewReaderDebug();
                    sw.Stop();
                    newTotal += sw.ElapsedMilliseconds;
                    Console.WriteLine($"{sw.Elapsed:mm\\:ss\\.ff}  ({newReqs} requests)");

                    Console.WriteLine();
                }

                Console.WriteLine("=== Results ===");
                Console.WriteLine($"[OLD] avg: {oldTotal / runs} ms");
                Console.WriteLine($"[NEW] avg: {newTotal / runs} ms");
                Console.WriteLine(
                    $"Saved:     {(oldTotal - newTotal) / runs} ms/upload on average"
                );
            }
            finally
            {
                bench.Cleanup();
            }
        }
    }
}
