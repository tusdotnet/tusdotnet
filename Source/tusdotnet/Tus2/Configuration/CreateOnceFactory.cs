using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal class CreateOnceFactory<T> : IAsyncDisposable where T : class
    {
        private readonly Func<HttpContext, Task<T>> _factory;
        private T _instance;

        public static CreateOnceFactory<T> Create(Func<HttpContext, Task<T>> factory)
        {
            return factory == null ? null : new(factory);
        }

        private CreateOnceFactory(Func<HttpContext, Task<T>> factory)
        {
            _factory = factory;
        }

        public async Task<T> Create(HttpContext context)
        {
            return _instance ??= await _factory(context);
        }

        public async ValueTask DisposeAsync()
        {
            if (_instance is IDisposable disposable)
                disposable.Dispose();
            else if (_instance is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            
        }
    }
}
