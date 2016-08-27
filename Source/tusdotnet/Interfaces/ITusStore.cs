using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
	public interface ITusStore
	{
		Task<long> AppendDataAsync(string fileName, Stream stream, CancellationToken cancellationToken);
		Task<bool> FileExistAsync(string fileName, CancellationToken cancellationToken);
		Task<long?> GetUploadLengthAsync(string fileName, CancellationToken cancellationToken);
		Task<long> GetUploadOffsetAsync(string fileName, CancellationToken cancellationToken);
	}
}