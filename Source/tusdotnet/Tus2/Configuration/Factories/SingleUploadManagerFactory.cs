using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal class SingleUploadManagerFactory : IUploadManagerFactory
    {
        private readonly IUploadManager _uploadManager;

        public SingleUploadManagerFactory(IUploadManager uploadManager)
        {
            _uploadManager = uploadManager;
        }

        public Task<IUploadManager> Create(HttpContext httpContext)
        {
            return Task.FromResult(_uploadManager);
        }
    }
}
