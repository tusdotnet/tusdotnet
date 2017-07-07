using System;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;
using tusdotnet.Models.Expiration;

namespace tusdotnet.Models
{
	/// <summary>
	/// The default tusdotnet configuration.
	/// </summary>
	public class DefaultTusConfiguration : ITusConfiguration
	{
		/// <inheritdoc />
		public string UrlPath { get; set; }

		/// <inheritdoc />
		public ITusStore Store { get; set; }

		/// <inheritdoc />
		public Func<string, ITusStore, CancellationToken, Task> OnUploadCompleteAsync { get; set; }

		/// <inheritdoc />
		public int? MaxAllowedUploadSizeInBytes { get; set; }

		/// <inheritdoc />
		public ExpirationBase Expiration { get; set; }
	}
}