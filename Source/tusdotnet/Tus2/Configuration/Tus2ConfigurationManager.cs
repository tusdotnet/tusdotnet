using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal class Tus2ConfigurationManager : ITus2ConfigurationManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public Tus2ConfigurationManager(IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor)
        {
            _serviceProvider = serviceProvider;
            _httpContextAccessor = httpContextAccessor;
        }

        public Task<ITus2Store> GetStore()
        {
            return GetStore(null);
        }

        public async Task<ITus2Store> GetStore(string name)
        {
            if (name == null)
                return _serviceProvider.GetService<ITus2Store>();

            var stores = _serviceProvider.GetServices<NamedFactory<ITus2StoreFactory>>();

            return await stores.First(s => s.Name == name).Factory.Create(_httpContextAccessor.HttpContext);
        }

        public Task<IUploadManager> GetUploadManager()
        {
            return GetUploadManager(null);
        }

        public async Task<IUploadManager> GetUploadManager(string name)
        {
            if (name == null)
                return _serviceProvider.GetService<IUploadManager>();

            var managers = _serviceProvider.GetServices<NamedFactory<IUploadManagerFactory>>();

            return await managers.First(m => m.Name == name).Factory.Create(_httpContextAccessor.HttpContext);
        }
    }
}
