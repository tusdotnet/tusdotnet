using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
	public interface ITusStore
	{
		Task AppendDataAsync(string fileName, byte[] data);
	}
}