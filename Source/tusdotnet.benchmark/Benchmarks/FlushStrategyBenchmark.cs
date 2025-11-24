using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using tusdotnet.Stores;

namespace tusdotnet.benchmark.Benchmarks
{
    /// <summary>
    /// Benchmark comparing flush strategies in TusDiskStore.AppendDataAsync:
    /// - Current (NEW): Write without flush after each write, single flush at end
    /// - Old: Write WITH flush after each write (old FlushToDisk behavior)
    ///
    /// Uses parameterized chunk sizes to exercise thresholds around the write and FileStream buffers.
    /// </summary>
    [SimpleJob()]
    [MemoryDiagnoser]
    //[ThreadingDiagnoser]
    public class FlushStrategyBenchmark
    {
        private const int IterationCount = 1;
        private string _tempDir;
        private TusDiskStore _store;
        private byte[] _testData;
        private string _fileId;

        [Params(5 * 1024 * 1024, 25 * 1024 * 1024, 100 * 1024 * 1024)]
        //[Params(1024 * 1024 * 1024)]
        public int FileSize { get; set; }

        // New parameter: how much data is sent to the store each write (chunk size)
        // Test values:
        //  - 49 KB: just under the write buffer size (50KB)
        //  - 51 KB: just over the write buffer size
        //  - 90 KB: just over FileStream internal buffer (84KB)
        //  - 167 KB: just under 2x FileStream internal buffer (2*84KB = 168KB)
        [Params(49 * 1024, 51 * 1024, 90 * 1024, 167 * 1024)]
        public int ChunkSize { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            //_tempDir = Path.Combine(Path.GetTempPath(), $"tusdotnet_benchmark_{Guid.NewGuid()}");
            _tempDir = Path.Combine("Z:\\", $"tusdotnet_benchmark_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);

            Console.WriteLine($"Generating {FileSize / (1024.0 * 1024.0):F1}MB test data...");
            _testData = new byte[FileSize];
            // Use a fixed seed for reproducible results
            new Random(42).NextBytes(_testData);
            Console.WriteLine("Test data ready.");
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch
            {
                // Ignore
            }
        }

        [IterationSetup]
        public void IterationSetup()
        {
            // Create a fresh store for each iteration
            _store = new TusDiskStore(_tempDir);
            _fileId = _store
                .CreateFileAsync(FileSize, null, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            // Clean up the file after each iteration
            try
            {
                var fileIdPath = Path.Combine(_tempDir, _fileId);
                if (File.Exists(fileIdPath))
                {
                    File.Delete(fileIdPath);
                }
                var metadataPath = fileIdPath + ".metadata";
                if (File.Exists(metadataPath))
                {
                    File.Delete(metadataPath);
                }
                var uploadLengthPath = fileIdPath + ".uploadlength";
                if (File.Exists(uploadLengthPath))
                {
                    File.Delete(uploadLengthPath);
                }
            }
            catch
            {
                // Ignore
            }
        }

        /// <summary>
        /// CURRENT (NEW): Uses the current AppendDataAsync implementation.
        /// FlushToDisk writes data WITHOUT calling FlushAsync after each write.
        /// Only a single FlushAsync at the end after all data is written.
        /// </summary>
        [Benchmark(Baseline = false, OperationsPerInvoke = IterationCount)]
        public async Task Current_NewFlushStrategy()
        {
            var pipe = new Pipe();
            var writeTask = FillPipeWithConsistentChunksAsync(pipe.Writer, _testData, ChunkSize);
            var readTask = _store.AppendDataAsync(_fileId, pipe.Reader, CancellationToken.None);
            await Task.WhenAll(writeTask, readTask);
        }

        /// <summary>
        /// OLD: Simulates the old AppendDataAsync behavior where FlushToDisk
        /// called FlushAsync after EVERY write that crossed the 50KB threshold.
        /// </summary>
        [Benchmark(Baseline = true, OperationsPerInvoke = IterationCount)]
        public async Task Old_FlushPerWrite()
        {
            var pipe = new Pipe();
            var writeTask = FillPipeWithConsistentChunksAsync(pipe.Writer, _testData, ChunkSize);
            var readTask = _store.Old_AppendDataAsync(_fileId, pipe.Reader, CancellationToken.None);
            await Task.WhenAll(writeTask, readTask);
        }

        /// <summary>
        /// Fills the pipe with consistent chunk sizes for reproducible benchmarking.
        /// </summary>
        private static async Task FillPipeWithConsistentChunksAsync(
            PipeWriter writer,
            byte[] preGeneratedData,
            int chunkSize
        )
        {
            var offset = 0;
            while (offset < preGeneratedData.Length)
            {
                var toWrite = Math.Min(chunkSize, preGeneratedData.Length - offset);

                var memory = writer.GetMemory(toWrite);
                preGeneratedData.AsMemory(offset, toWrite).CopyTo(memory);
                writer.Advance(toWrite);
                await writer.FlushAsync();

                offset += toWrite;
            }

            await writer.CompleteAsync();
        }
    }
}
