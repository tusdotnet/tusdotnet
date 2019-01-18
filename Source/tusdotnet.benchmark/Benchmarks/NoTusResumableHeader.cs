using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using tusdotnet.Adapters;

namespace tusdotnet.benchmark.Benchmarks
{
    [MemoryDiagnoser, HtmlExporter, CsvExporter]
    public class NoTusResumableHeader
    {
        private ContextAdapter _createRequest;
        private HttpStatusCode _statusCode;
        private bool _onAuthorizeCalled;
        private bool _onOnBeforeCreateCalled;
        private bool _onCreateCompleteCalled;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var config = CreateConfig();
            _createRequest = new ContextAdapter
            {
                CancellationToken = CancellationToken.None,
                Configuration = config,
                Request = new RequestAdapter(config.UrlPath)
                {
                    Headers = new Dictionary<string, List<string>>
                    {
                        {"Content-Type", new List<string> { "application/offset+octet-stream" } },
                        {"Upload-Length", new List<string> { "10" } }
                    },
                    Method = "post",
                    RequestUri = new Uri("http://localhost:5000/files")
                },
                Response = new ResponseAdapter
                {
                    Body = new MemoryStream(),
                    SetHeader = SetHeader,
                    SetStatus = SetStatus
                }
            };
        }

        [Benchmark(Baseline = true)]
        public async Task NoTusResumableHeaderMethodBased()
        {
            await TusProtocolHandler.Invoke(_createRequest);
            AssertStatusCode(0);
            AssertAuthorizeCalled();
            AssertBeforeCreateCalled();
            AssertCreateCompleteCalled();
        }

        [Benchmark]
        public async Task NoTusResumableHeaderIntentBased()
        {
            await TusProtocolHandlerIntentBased.Invoke(_createRequest);
            AssertStatusCode(0);
            AssertAuthorizeCalled();
            AssertBeforeCreateCalled();
            AssertCreateCompleteCalled();
        }

        private void SetStatus(HttpStatusCode status)
        {
            _statusCode = status;
        }

        private void SetHeader(string key, string value)
        {
        }

        private void AssertStatusCode(HttpStatusCode status)
        {
            if (_statusCode != status)
                throw new Exception("Request failed");
        }

        private void AssertAuthorizeCalled()
        {
            if (_onAuthorizeCalled != false)
                throw new Exception("OnAuthorize should have been called " + false + " but the inverse happened");
        }

        private void AssertBeforeCreateCalled()
        {
            if (_onOnBeforeCreateCalled)
                throw new Exception("BeforeCreate was called but should not have been");
        }

        private void AssertCreateCompleteCalled()
        {
            if (_onCreateCompleteCalled)
                throw new Exception("CreateComplete was called but should not have been");
        }

        private Models.DefaultTusConfiguration CreateConfig()
        {
            return new Models.DefaultTusConfiguration
            {
                Store = new InMemoryStore(),
                UrlPath = "/files",
                Events = new Models.Configuration.Events
                {
                    OnAuthorizeAsync = ctx => { _onAuthorizeCalled = true; return Task.CompletedTask; },
                    OnBeforeCreateAsync = ctx => { _onOnBeforeCreateCalled = true; return Task.CompletedTask; },
                    OnCreateCompleteAsync = ctx => { _onCreateCompleteCalled = true; return Task.CompletedTask; }
                }
            };
        }
    }
}
