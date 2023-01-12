#if NET6_0_OR_GREATER
using System;
using tusdotnet.Adapters;
using tusdotnet.Models;

namespace tusdotnet.Runners.TusV1Process
{
    public class DeleteFileRequest : TusV1Request
    {
        public string FileId { get; set; }

        internal ContextAdapter ToContextAdapter(DefaultTusConfiguration config)
        {
            var adapter = new ContextAdapter("/", EndpointUrlHelper.Instance)
            {
                Request = new()
                {
                    Body = null,
                    BodyReader = null,
                    Method = "delete",
                    RequestUri = new Uri("/" + FileId, UriKind.Relative),
                    Headers = RequestHeaders.FromDictionary(new())
                },
                Response = new(),
                Configuration = config,
                CancellationToken = CancellationToken,
                FileId = FileId
            };

            return adapter;
        }

        internal static DeleteFileRequest FromContextAdapter(ContextAdapter context)
        {
            return new()
            {
                FileId = context.FileId,
                CancellationToken = context.CancellationToken
            };
        }
    }
}
#endif