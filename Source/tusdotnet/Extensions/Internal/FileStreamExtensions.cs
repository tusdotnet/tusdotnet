using System.IO;
using System.Security.Cryptography;

namespace tusdotnet.Extensions
{
	internal static class FileStreamExtensions
	{
#if netfull

		public static byte[] CalculateSha1(this FileStream fileStream, long chunkStartPosition)
		{
			byte[] fileHash;
			using (var sha1 = new SHA1Managed())
			{
			    var originalPos = fileStream.Position;
			    fileStream.Seek(chunkStartPosition, SeekOrigin.Begin);
				fileHash = sha1.ComputeHash(fileStream);
			    fileStream.Seek(originalPos, SeekOrigin.Begin);
			}

			return fileHash;
		}

#endif

#if netstandard || NETCOREAPP3_0

		public static byte[] CalculateSha1(this FileStream fileStream, long chunkStartPosition)
		{
			byte[] fileHash;
			using (var sha1 = SHA1.Create())
			{
			    var originalPos = fileStream.Position;
			    fileStream.Seek(chunkStartPosition, SeekOrigin.Begin);
				fileHash = sha1.ComputeHash(fileStream);
			    fileStream.Seek(originalPos, SeekOrigin.Begin);
			}

			return fileHash;
		}

#endif
	}
}
