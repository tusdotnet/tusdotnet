using System;
using System.IO;

namespace tusdotnet.Adapters
{
	/// <summary>
	/// Response wrapper that handles different pipeline responses.
	/// </summary>
	internal class ResponseAdapter
	{
		public Action<int> SetStatus { get; set; }

		public Stream Body { get; set; }

		public Action<string, string> SetHeader { get; set; }
	}
}