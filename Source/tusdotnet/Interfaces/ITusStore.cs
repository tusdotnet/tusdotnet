using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
	public interface ITusStore
	{
		Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken);
		Task<bool> FileExistAsync(string fileId, CancellationToken cancellationToken);
		Task<long?> GetUploadLengthAsync(string fileId, CancellationToken cancellationToken);
		Task<long> GetUploadOffsetAsync(string fileId, CancellationToken cancellationToken);
	}
}