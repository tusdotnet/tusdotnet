using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Adapters
{
    internal class ProtocolHandlerUrlHelper : IUrlHelper
    {
        private readonly TusContext _tusContext;

        public ProtocolHandlerUrlHelper(TusContext context)
        {
            _tusContext = context;
        }

        public string ParseFileId(ContextAdapter context)
        {
            return _tusContext.FileId;
        }

        public bool UrlMatchesFileIdUrl(ContextAdapter context)
        {
            return HasFileId(_tusContext);
        }

        public bool UrlMatchesUrlPath(ContextAdapter context)
        {
            return !HasFileId(_tusContext);
        }

        private static bool HasFileId(TusContext context) =>
            !string.IsNullOrWhiteSpace(context.FileId);
    }
}
