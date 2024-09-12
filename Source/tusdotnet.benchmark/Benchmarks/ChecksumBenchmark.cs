//using BenchmarkDotNet.Attributes;
//using Dia2Lib;
//using System;
//using System.Buffers;
//using System.Collections.Generic;
//using System.IO;
//using System.Security.Cryptography;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

//namespace tusdotnet.benchmark.Benchmarks
//{
//    [MemoryDiagnoser]
//    public class ChecksumBenchmark
//    {
//        private byte[] _data;

//        public ChecksumBenchmark()
//        {
//            _data = new byte[100 * 1024 * 1024];
//            Random.Shared.NextBytes(_data);
//        }

//        [Benchmark(Baseline = true)]
//        public async Task<object> Old()
//        {
//            var path = @"Z:\" + Guid.NewGuid().ToString();
//            byte[] returnData;
//            using (var fileStream = File.OpenWrite(path))
//            {
//                var _maxReadBufferSize = 51200;
//                int readFromClient;
//                var httpReadBuffer = ArrayPool<byte>.Shared.Rent(_maxReadBufferSize);
//                var stream = new MemoryStream(_data, false);
//                do
//                {
//                    readFromClient = await stream.ReadAsync(httpReadBuffer, 0, _maxReadBufferSize, CancellationToken.None);

//                    await fileStream.WriteAsync(httpReadBuffer, 0, readFromClient);

//                    returnData = httpReadBuffer;

//                } while (readFromClient != 0);
//            }

//            byte[] fileHash;
//            using (var sha1 = SHA1.Create())
//            {
//                using var fs = File.OpenRead(path);
//                fileHash = sha1.ComputeHash(fs);
//            }

//            return (fileHash, returnData);
//        }

//        [Benchmark]
//        public async Task<object> New()
//        {
//            using var sha1 = SHA1.Create();
//            using var fileStream = File.OpenWrite(@"Z:\" + Guid.NewGuid().ToString());
//            byte[] returnData;
//            var _maxReadBufferSize = 51200;
//            int readFromClient;
//            var httpReadBuffer = ArrayPool<byte>.Shared.Rent(_maxReadBufferSize);
//            var stream = new MemoryStream(_data, false);
//            do
//            {
//                readFromClient = await stream.ReadAsync(httpReadBuffer, 0, _maxReadBufferSize, CancellationToken.None);
//                await fileStream.WriteAsync(httpReadBuffer, 0, readFromClient);

//                sha1.TransformBlock(httpReadBuffer, 0, readFromClient, null, 0);

//                returnData = httpReadBuffer;

//            } while (readFromClient != 0);

//            sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

//            return (sha1.Hash, returnData);
//        }
//    }
//}
