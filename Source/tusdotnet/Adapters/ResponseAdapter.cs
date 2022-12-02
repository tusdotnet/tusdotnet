using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace tusdotnet.Adapters
{
	/// <summary>
	/// Response wrapper that handles different pipeline responses.
	/// </summary>
	internal class ResponseAdapter
	{
		internal Action<HttpStatusCode> SetStatus { get; set; }

		internal Stream Body { get; set; }

		internal Action<string, string> SetHeader { get; set; }

        public ResponseAdapter()
        {
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _stream = new MemoryStream();

            SetStatus = status => Status = status;
            SetHeader = (key, value) => Headers[key] = value;
            Body = _stream;
        }

        internal HttpStatusCode Status { get; private set; }

        internal Dictionary<string, string> Headers { get; }

        private readonly MemoryStream _stream;
    }
}