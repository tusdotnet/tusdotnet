using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Helpers;
using tusdotnet.Helpers.Internal;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;

namespace tusdotnet.benchmark.Benchmarks
{
    [MemoryDiagnoser]
    public class EventHelperBenchmark
    {
        private static ContextAdapter _context = new ContextAdapter
        {
            Request = new RequestAdapter("/files")
            {
                RequestUri = new Uri("http://localhost/files/file1")
            },
            Configuration = new DefaultTusConfiguration
            {
                Events = new Events
                {
                    OnAuthorizeAsync = ctx =>
                    {
                        return Task.CompletedTask;
                    },

                    OnBeforeCreateAsync = ctx =>
                    {
                        return Task.CompletedTask;
                    },
                    OnCreateCompleteAsync = ctx =>
                    {
                        return Task.CompletedTask;
                    },
                    OnBeforeDeleteAsync = ctx =>
                    {
                        return Task.CompletedTask;
                    },
                    OnDeleteCompleteAsync = ctx =>
                    {
                        return Task.CompletedTask;
                    },
                    OnFileCompleteAsync = ctx =>
                    {
                        return Task.CompletedTask;
                    }
                },
            },
            CancellationToken = System.Threading.CancellationToken.None
        };

        [Benchmark(Baseline = true)]
        public async Task EventHelperReflectionBased()
        {
            await EventHelper.Validate<AuthorizeContext>(_context);
            await EventHelper.Validate<BeforeCreateContext>(_context);
            await EventHelper.Validate<BeforeDeleteContext>(_context);

            await EventHelper.Notify<CreateCompleteContext>(_context);
            await EventHelper.Notify<DeleteCompleteContext>(_context);

            await EventHelper.NotifyFileComplete(_context);
        }

        [Benchmark]
        public async Task EventHelperPatternMatchingBased()
        {
            await EventHelperPatternMatching.Validate<AuthorizeContext>(_context);
            await EventHelperPatternMatching.Validate<BeforeCreateContext>(_context);
            await EventHelperPatternMatching.Validate<BeforeDeleteContext>(_context);

            await EventHelperPatternMatching.Notify<CreateCompleteContext>(_context);
            await EventHelperPatternMatching.Notify<DeleteCompleteContext>(_context);

            await EventHelperPatternMatching.NotifyFileComplete(_context);
        }
    }
}
