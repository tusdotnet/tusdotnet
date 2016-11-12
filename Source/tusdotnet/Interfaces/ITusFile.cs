using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
	public interface ITusFile
	{
		string Id { get; }
		Task<Stream> GetContent(CancellationToken cancellationToken);
		Dictionary<string, string> Metadata { get; }
	}
}