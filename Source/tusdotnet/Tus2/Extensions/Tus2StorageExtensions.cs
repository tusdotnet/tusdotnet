using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal static class Tus2StorageExtensions
    {
        internal static async Task<long?> TryGetOffset(this Tus2Storage storage, string uploadToken)
        {
            try
            {
                return await storage.GetOffset(uploadToken);
            }
            catch
            {
                return null;
            }
        }
    }
}
