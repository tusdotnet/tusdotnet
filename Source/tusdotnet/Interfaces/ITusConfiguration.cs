using System;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
	public interface ITusConfiguration
	{
		string UrlPath { get; }
		ITusStore Store { get; }
		Func<string, ITusStore, CancellationToken, Task> OnUploadCompleteAsync { get; }
	}
}