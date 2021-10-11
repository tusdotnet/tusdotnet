using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using tusdotnet.Models.Concatenation;

namespace tusdotnet.benchmark.Benchmarks
{
    [MemoryDiagnoser]
    public class UploadConcatParserBenchmark
    {
        private const string _uploadConcatHeader = "final;/files/partial1 http://localhost:1321/files/partial2";
        private const string _urlPath = "/files";

        [Benchmark(Baseline = true)]
        public FileConcat UploadConcatParserStringBased()
        {
            return Parsers.UploadConcatParserHelpers.UploadConcatParserStringBased.ParseAndValidate(_uploadConcatHeader, _urlPath).Type;
        }

        [Benchmark()]
        public FileConcat UploadConcatParserSpanBased()
        {
            return Parsers.UploadConcatParserHelpers.UploadConcatParserSpanBased.ParseAndValidate(_uploadConcatHeader, _urlPath).Type;
        }


    }
}
