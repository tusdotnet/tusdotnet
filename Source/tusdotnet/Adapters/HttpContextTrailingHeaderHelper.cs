#if trailingheaders
#nullable enable

using Microsoft.AspNetCore.Http;
using System.Linq;
using tusdotnet.Constants;

namespace tusdotnet.Adapters
{
    internal class HttpContextTrailingHeaderHelper : ITrailingHeaderHelper
    {
        private readonly HttpContext _context;

        public HttpContextTrailingHeaderHelper(HttpContext context)
        {
            _context = context;
        }

        public string? GetTrailingUploadChecksumHeader()
        {
            var httpRequest = _context.Request;

            if (!httpRequest.SupportsTrailers() || !httpRequest.CheckTrailersAvailable())
                return null;

            if (!HasDeclaredTrailingUploadChecksumHeader())
                return null;

            return httpRequest.GetTrailer(HeaderConstants.UploadChecksum).FirstOrDefault();
        }

        public bool HasDeclaredTrailingUploadChecksumHeader()
        {
            return _context
                .Request.GetDeclaredTrailers()
                .Any(x => x == HeaderConstants.UploadChecksum);
        }
    }
}

#endif
