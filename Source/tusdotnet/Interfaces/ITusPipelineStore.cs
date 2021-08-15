using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Models;

namespace tusdotnet.Interfaces
{
#if NETCOREAPP3_0
	/// <summary>
	/// Support for System.IO.Pipelines
	/// </summary>
	public interface ITusPipelineStore
    {
		/// <summary>
		/// Write data to the file using the provided pipe reader.
		/// The implementation must throw <exception cref="TusStoreException"></exception> 
		/// if the pipe readers length exceeds the upload length of the file.
		/// </summary>
		/// <param name="fileId">The id of the file to write</param>
		/// <param name="pipeReader">The request input pipe reader from the client</param>
		/// <param name="cancellationToken">Cancellation token to use when cancelling</param>
		/// <returns>The number of bytes written</returns>
		Task<long> AppendDataAsync(string fileId, System.IO.Pipelines.PipeReader pipeReader, CancellationToken cancellationToken);

	}
#endif
}
