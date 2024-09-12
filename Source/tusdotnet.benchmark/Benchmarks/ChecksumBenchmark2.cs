using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.benchmark.Benchmarks
{
    [MemoryDiagnoser]
    public class ChecksumBenchmark2
    {
        private readonly byte[] _data;
        private static string _path = @"Z:\00bed497-2b4d-4941-9c9e-d299d54a78c9";
        public ChecksumBenchmark2()
        {
            _data = File.ReadAllBytes(_path);
        }

        [Benchmark(Baseline = true)]
        public byte[] ReadFromFile()
        {
            byte[] fileHash;
            using (var sha1 = SHA1.Create())
            {
                using var fs = File.OpenRead(_path);
                fileHash = sha1.ComputeHash(fs);
            }

            return fileHash;
        }

        [Benchmark]
        public async Task<byte[]> ReadFromClient()
        {
            using var sha1 = SHA1.Create();
            byte[] returnData;
            var _maxReadBufferSize = 51200;
            int readFromClient;
            var httpReadBuffer = ArrayPool<byte>.Shared.Rent(_maxReadBufferSize);
            var stream = new MemoryStream(_data, false);
            do
            {
                readFromClient = await stream.ReadAsync(httpReadBuffer, 0, _maxReadBufferSize, CancellationToken.None);
                
                sha1.TransformBlock(httpReadBuffer, 0, readFromClient, null, 0);

                returnData = httpReadBuffer;

            } while (readFromClient != 0);

            sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            return sha1.Hash;
        }
    }
}
