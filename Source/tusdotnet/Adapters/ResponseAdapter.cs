#nullable enable
using System;
using System.Collections.Generic;
using System.Net;

namespace tusdotnet.Adapters
{
    /// <summary>
    /// Response wrapper that handles different pipeline responses.
    /// </summary>
    internal class ResponseAdapter
    {
        internal ResponseAdapter()
        {
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        internal void SetResponse(HttpStatusCode status, string? message = null)
        {
            Status = status;
            Message = message;
        }

        internal void SetHeader(string key, string value)
        {
            Headers[key] = value;
        }

        internal HttpStatusCode Status { get; private set; }

        /// <summary>True if a response has been set, false if the request was aborted without a response (e.g. client disconnect).</summary>
        internal bool HasResponse => Status != 0;

        internal string? Message { get; private set; }

        internal Dictionary<string, string> Headers { get; }
    }
}
