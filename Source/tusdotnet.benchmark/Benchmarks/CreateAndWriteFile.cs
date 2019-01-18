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
    public class CreateAndWriteFile
    {
        private ContextAdapter _createRequest;
        private ContextAdapter _writeFileRequest;

        private Dictionary<string, string> _createResponseHeaders;

        private HttpStatusCode _statusCode;
        private bool _onAuthorizeCalled;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var config = CreateConfig();

            _createResponseHeaders = new Dictionary<string, string>();

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
                    SetHeader = SetHeaderCreate,
                    SetStatus = SetStatus
                }
            };

            _writeFileRequest = new ContextAdapter
            {
                CancellationToken = CancellationToken.None,
                Configuration = config,
                Request = new RequestAdapter(config.UrlPath)
                {
                    Headers = new Dictionary<string, List<string>>
                    {
                        { "Tus-Resumable", new List<string> { "1.0.0" } },
                        { "Upload-Offset", new List<string> { "0" } },
                        { "Content-Type", new List<string> { "application/offset+octet-stream" } }
                    },
                    Method = "patch",
                    Body = new MemoryStream(new byte[10])
                },
                Response = new ResponseAdapter
                {
                    Body = new MemoryStream(),
                    SetHeader = SetHeaderNoop,
                    SetStatus = SetStatus
                }
            };
        }

        private void SetStatus(HttpStatusCode status)
        {
            _statusCode = status;
        }

        private void SetHeaderCreate(string key, string value)
        {
            _createResponseHeaders[key] = value;
        }

        private void SetHeaderNoop(string key, string value)
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
                    OnAuthorizeAsync = ctx => { _onAuthorizeCalled = true; return Task.CompletedTask; }
                }
            };
        }

        [Benchmark(Baseline = true)]
        public async Task CreateWriteDeleteFileMethodBased()
        {
            await TusProtocolHandler.Invoke(_createRequest);
            AssertStatusCode(HttpStatusCode.Created);
            AssertAuthorizeCalled(false);

            _writeFileRequest.Request.RequestUri = GetFileUrl();
            await TusProtocolHandler.Invoke(_writeFileRequest);
            AssertStatusCode(HttpStatusCode.NoContent);
            AssertAuthorizeCalled(false);
        }

        [Benchmark]
        public async Task CreateWriteDeleteFileIntentBased()
        {
            await TusProtocolHandlerIntentBased.Invoke(_createRequest);
            AssertStatusCode(HttpStatusCode.Created);
            AssertAuthorizeCalled(true);

            _writeFileRequest.Request.RequestUri = GetFileUrl();
            await TusProtocolHandlerIntentBased.Invoke(_writeFileRequest);
            AssertStatusCode(HttpStatusCode.NoContent);
            AssertAuthorizeCalled(true);
        }

        private Uri GetFileUrl()
        {
            return new Uri("http://localhost:5000" + _createResponseHeaders["location"]);
        }

        private void AssertStatusCode(HttpStatusCode status)
        {
            if (_statusCode != status)
                throw new Exception("Request failed");

            _statusCode = 0;
        }

        private void AssertAuthorizeCalled(bool shouldHaveBeenCalled = true)
        {
            if (_onAuthorizeCalled != shouldHaveBeenCalled)
                throw new Exception("OnAuthorize should have been called " + shouldHaveBeenCalled + " but the inverse happened");

            _onAuthorizeCalled = false;
        }
    }
}
