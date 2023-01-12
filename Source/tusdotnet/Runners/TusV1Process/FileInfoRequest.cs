#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Components.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Models;

namespace tusdotnet.Runners
{
    public class FileInfoRequest : TusV1Request
    {
        public string FileId { get; set; }

        internal ContextAdapter ToContextAdapter(DefaultTusConfiguration config)
        {
            return new("/", EndpointUrlHelper.Instance)
            {
                Request = new()
                {
                    Headers = RequestHeaders.FromDictionary(new()),
                    Method = "head",
                    RequestUri = new Uri("/" + FileId, UriKind.Relative)
                },
                Response = new(),
                CancellationToken = CancellationToken,
                Configuration = config,
                FileId = FileId
            };
        }

        internal static FileInfoRequest FromContextAdapter(ContextAdapter contextAdapter)
        {
            return new()
            {
                CancellationToken = contextAdapter.CancellationToken,
                FileId = contextAdapter.FileId
            };
        }
    }
}
#endif