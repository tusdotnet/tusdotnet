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
    public class ChecksumBenchmark3
    {
        private byte[] _data;

        public ChecksumBenchmark3()
        {
            _data = new byte[10 * 1024 * 1024];
            Random.Shared.NextBytes(_data);
        }

        [Benchmark(Baseline = true)]
        public async Task<byte[]> NoHashing()
        {
            byte[] returnData;
            var _maxReadBufferSize = 51200;
            int readFromClient;
            var httpReadBuffer = ArrayPool<byte>.Shared.Rent(_maxReadBufferSize);
            var stream = new MemoryStream(_data, false);
            do
            {
                readFromClient = await stream.ReadAsync(httpReadBuffer, 0, _maxReadBufferSize, CancellationToken.None);

                returnData = httpReadBuffer;

            } while (readFromClient != 0);

            return returnData;
        }

        [Benchmark]
        public async Task<byte[]> HashingDuringRead()
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
