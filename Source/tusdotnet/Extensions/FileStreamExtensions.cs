using System.IO;
using System.Security.Cryptography;

namespace tusdotnet.Extensions
{
	internal static class FileStreamExtensions
	{
#if netfull

		public static byte[] CalculateSha1(this FileStream fileStream)
		{
			byte[] fileHash;
			using (var sha1 = new SHA1Managed())
			{
				fileHash = sha1.ComputeHash(fileStream);
			}

			return fileHash;
		}

#endif

#if netstandard

		public static byte[] CalculateSha1(this FileStream fileStream)
		{
			byte[] fileHash;
			using (var sha1 = SHA1.Create())
			{
				fileHash = sha1.ComputeHash(fileStream);
			}

			return fileHash;
		}

#endif
	}
}
