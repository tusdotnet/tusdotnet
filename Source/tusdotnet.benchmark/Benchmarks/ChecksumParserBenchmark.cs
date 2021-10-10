using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using tusdotnet.Models;

namespace tusdotnet.benchmark.Benchmarks
{
    [MemoryDiagnoser]
    public class ChecksumParserBenchmark
    {
        private const string header = "sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0=";

        [Benchmark(Baseline = true)]
        public bool ChecksumParserStringBased() => Parsers.ChecksumParserHelpers.ChecksumParserStringBased.ParseAndValidate(header).Success;

        [Benchmark]
        public bool ChecksumParserSpanBased() => Parsers.ChecksumParserHelpers.ChecksumParserSpanBased.ParseAndValidate(header).Success;
    }
}
