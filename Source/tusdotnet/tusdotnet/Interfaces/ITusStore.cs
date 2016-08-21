using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
	public interface ITusStore
	{
		Task AppendDataAsync(string fileName, byte[] data);
		Task<bool> FileExistAsync(string fileName);
		Task<long?> GetUploadLengthAsync(string fileName);
		Task<long> GetUploadOffsetAsync(string fileName);
	}
}