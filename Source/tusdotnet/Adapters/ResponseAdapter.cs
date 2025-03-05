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

        internal string? Message { get; private set; }

        internal Dictionary<string, string> Headers { get; }
    }
}
