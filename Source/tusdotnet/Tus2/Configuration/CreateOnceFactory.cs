using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal class CreateOnceFactory<T> : IAsyncDisposable where T : class
    {
        private readonly Func<HttpContext, Task<T>> _factory;
        private T _instance;

        public CreateOnceFactory(Func<HttpContext, Task<T>> factory)
        {
            _factory = factory;
        }

        public async Task<T> Create(HttpContext context)
        {
            return _instance ??= await _factory(context);
        }

        public async ValueTask DisposeAsync()
        {
            if (_instance == null)
                return;

            if (_instance is IDisposable disposable)
                disposable.Dispose();
            else if (_instance is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            
        }
    }
}
