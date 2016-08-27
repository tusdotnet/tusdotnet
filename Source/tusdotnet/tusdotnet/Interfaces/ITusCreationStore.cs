using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
	public interface ITusCreationStore
	{
		Task<string> CreateFileAsync(long uploadLength, CancellationToken cancellationToken);
	}
}