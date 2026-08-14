#if trailingheaders
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tusdotnet.Constants;

namespace tusdotnet.Adapters
{
    internal class TusContextTrailingHeaderHelper : ITrailingHeaderHelper
    {
        private readonly TusContext _context;

        public TusContextTrailingHeaderHelper(TusContext context)
        {
            _context = context;
        }

        public string GetTrailingUploadChecksumHeader()
        {
            if (!_context.SupportsTrailingHeaders || !_context.CheckTrailingHeadersAvailable())
                return null;

            if (!HasDeclaredTrailingUploadChecksumHeader())
                return null;

            return _context.GetTrailingHeader(HeaderConstants.UploadChecksum);
        }

        public bool HasDeclaredTrailingUploadChecksumHeader()
        {
            // TODO: Optimize
            return _context.Headers.TryGetValue("Trailer", out var trailerHeader)
                && trailerHeader
                    .Split(',')
                    .Select(x => x.Trim())
                    .Contains(HeaderConstants.UploadChecksum);
        }
    }
}
#endif
