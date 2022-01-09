using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal class SingleStoreFactory : ITus2StoreFactory
    {
        private readonly ITus2Store _store;

        public SingleStoreFactory(ITus2Store store)
        {
            _store = store;
        }

        public Task<ITus2Store> Create(HttpContext httpContext)
        {
            return Task.FromResult(_store);
        }
    }
}
