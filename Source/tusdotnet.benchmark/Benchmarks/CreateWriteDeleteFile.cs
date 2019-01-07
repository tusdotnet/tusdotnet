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
    [MemoryDiagnoser]
    public class CreateWriteDeleteFile
    {
        private readonly ContextAdapter _createRequest;

        private HttpStatusCode _statusCode;
        private bool _onAuthorizeCalled;
        private bool _onOnBeforeCreateCalled;
        private bool _onCreateCompleteCalled;

        public CreateWriteDeleteFile()
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
                        {"Tus-Resumable", new List<string> { "1.0.0" } },
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

        private void SetStatus(HttpStatusCode status)
        {
            _statusCode = status;
        }

        private void SetHeader(string key, string value)
        {
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

        [Benchmark]
        public async Task CreateMethodBased()
        {
            await TusProtocolHandler.Invoke(_createRequest);
            AssertStatusCode(HttpStatusCode.Created);
            // Method based invoke does not call OnAuthorize
            AssertBeforeCreateCalled();
            AssertCreateCompleteCalled();
        }

        [Benchmark]
        public async Task CreateIntentBased()
        {
            await TusProtocolHandlerIntentBased.Invoke(_createRequest);
            AssertStatusCode(HttpStatusCode.Created);
            AssertAuthorizeCalled();
            AssertBeforeCreateCalled();
            AssertCreateCompleteCalled();
        }

        private void AssertStatusCode(HttpStatusCode status)
        {
            if (_statusCode != status)
                throw new Exception("Request failed");
        }

        private void AssertAuthorizeCalled()
        {
            if (!_onAuthorizeCalled)
                throw new Exception("OnAuthorize was not called");
        }

        private void AssertBeforeCreateCalled()
        {
            if (!_onOnBeforeCreateCalled)
                throw new Exception("BeforeCreate was not called");
        }

        private void AssertCreateCompleteCalled()
        {
            if (!_onCreateCompleteCalled)
                throw new Exception("CreateComplete was not called");
        }
    }
}
