using System;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
	/// <summary>
	/// Configuration used for tusdotnet
	/// </summary>
	public interface ITusConfiguration
	{
		/// <summary>
		/// The url path to listen for uploads on, e.g. "/files"
		/// </summary>
		string UrlPath { get; }

		/// <summary>
		/// The store to use when storing files
		/// </summary>
		ITusStore Store { get; }

		/// <summary>
		/// Callback ran when a file is completely uploaded. 
		/// </summary>
		Func<string, ITusStore, CancellationToken, Task> OnUploadCompleteAsync { get; }

		/// <summary>
		/// The maximum upload size to allow. Exceeding this limit will return an error to the client.
		/// </summary>
		int? MaxAllowedUploadSizeInBytes { get; }
	}
}