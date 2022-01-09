using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public interface IUploadManagerFactory
    {
        public Task<IUploadManager> Create(HttpContext httpContext);
    }
}
