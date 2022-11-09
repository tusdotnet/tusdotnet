using System;
using System.IO;
#if pipelines
using System.IO.Pipelines;
#endif

namespace tusdotnet.Adapters
{
    /// <summary>
    /// Request adapter that handles different pipeline requests.
    /// </summary>
    internal sealed class RequestAdapter
    {
        public string Method { get; set; }

        public Uri RequestUri { get; set; }

        public Stream Body { get; set; }

#if pipelines
        public PipeReader BodyReader { get; set; }
#endif

        public RequestHeaders Headers { get; set; }
    }
}