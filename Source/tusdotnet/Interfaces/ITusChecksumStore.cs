using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
	public interface ITusChecksumStore
	{
		Task<IEnumerable<string>> GetSupportedAlgorithmsAsync(CancellationToken cancellationToken);
		Task<bool> VerifyChecksumAsync(string fileId, string algorithm, byte[] checksum, CancellationToken cancellationToken);
	}
}
