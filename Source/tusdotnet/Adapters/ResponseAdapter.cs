using System;
using System.IO;

namespace tusdotnet.Adapters
{
	internal class ResponseAdapter
	{
		public Action<int> SetStatus { get; set; }
		public Stream Body { get; set; }
		public Action<string, string> SetHeader { get; set; }
	}
}
