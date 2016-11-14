using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Models;

namespace tusdotnet.Interfaces
{
	public interface ITusFile
	{
		string Id { get; }
		Task<Stream> GetContentAsync(CancellationToken cancellationToken);
		Task<Dictionary<string, Metadata>> GetMetadataAsync(CancellationToken cancellationToken);
	}
}