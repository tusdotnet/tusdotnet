using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
	public interface ITusReadableStore
	{
		Task<ITusFile> GetFileAsync(string fileId, CancellationToken cancellationToken);
	}
}