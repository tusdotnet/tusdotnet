using System;
using System.IO;
using System.Net;

namespace tusdotnet.Adapters
{
	/// <summary>
	/// Response wrapper that handles different pipeline responses.
	/// </summary>
	internal sealed class ResponseAdapter
	{
		internal Action<HttpStatusCode> SetStatus { get; set; }

		internal Stream Body { get; set; }

		internal Action<string, string> SetHeader { get; set; }
	}
}