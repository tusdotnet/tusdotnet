using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;
using tusdotnet.Models;

namespace tusdotnet.benchmark.Benchmarks
{
    [MemoryDiagnoser, HtmlExporter, CsvExporter]
    public class RequestIsNotForTusEndpoint
    {
        private bool _onAuthorizeCalled;
        private bool _onOnBeforeCreateCalled;
        private bool _onCreateCompleteCalled;
        private HttpContext _httpContext;
        private bool _nextWasCalled;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _httpContext = new DefaultHttpContext();
            _httpContext.Request.Method = "POST";
            _httpContext.Request.Host = new HostString("localhost");
            _httpContext.Request.Scheme = "http";
            _httpContext.Request.Path = "/someotherpath";
            _httpContext.Request.Headers["Tus-Resumable"] = "1.0.0";
            _httpContext.Request.Headers["Upload-Length"] = "100";
        }

        private DefaultTusConfiguration CreateConfig()
        {
            return new DefaultTusConfiguration
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

        [Benchmark(Baseline = true)]
        public async Task RequestIsNotForTusEndpointMethodBased()
        {
            await new TusCoreMiddlewareOld(Next, ConfigFactory).Invoke(_httpContext);
            AssertNextWasCalled();
            AssertAuthorizeCalled();
            AssertBeforeCreateCalled();
            AssertCreateCompleteCalled();
        }

        [Benchmark]
        public async Task RequestIsNotForTusEndpointIntentBased()
        {
            await new TusCoreMiddleware(Next, ConfigFactory).Invoke(_httpContext);
            AssertNextWasCalled();
            AssertAuthorizeCalled();
            AssertBeforeCreateCalled();
            AssertCreateCompleteCalled();
        }

        private Task Next(HttpContext _)
        {
            _nextWasCalled = true;
            return Task.CompletedTask;
        }

        private Task<DefaultTusConfiguration> ConfigFactory(HttpContext _) => Task.FromResult(CreateConfig());

        private void AssertNextWasCalled()
        {
            if (!_nextWasCalled)
                throw new Exception("Next was not called");
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
    }
}
