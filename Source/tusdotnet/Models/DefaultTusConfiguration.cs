using System;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;

namespace tusdotnet.Models
{
	public class DefaultTusConfiguration : ITusConfiguration
	{
		public string UrlPath { get; set; }
		public ITusStore Store { get; set; }
		public Func<string, ITusStore, CancellationToken, Task> OnUploadCompleteAsync { get; set; }
	}
}
