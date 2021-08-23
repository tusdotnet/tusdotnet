using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;

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

#if netstandard

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

#if pipelines

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task FlushToDisk(this FileStream stream, ReadOnlySequence<byte> buffer)
        {
            foreach (var segment in buffer)
            {
                await stream.WriteAsync(segment);
            }

            await stream.FlushAsync();
        }

#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task FlushFileToDisk(this FileStream fileStream, byte[] fileWriteBuffer, int writeBufferNextFreeIndex)
        {
            await fileStream.WriteAsync(fileWriteBuffer, 0, writeBufferNextFreeIndex);
            await fileStream.FlushAsync();
        }

    }
}
