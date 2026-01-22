using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Extensions;

namespace tusdotnet.Stores
{
    /// <summary>
    /// Expiration-related functionality for TusDiskStore (ITusExpirationStore implementation).
    /// </summary>
    public partial class TusDiskStore
    {
        /// <inheritdoc />
        public async Task SetExpirationAsync(
            string fileId,
            DateTimeOffset expires,
            CancellationToken _
        )
        {
            _fileRepFactory
                .Expiration(await InternalFileId.Parse(_fileIdProvider, fileId))
                .Write(expires.ToString("O"));
        }

        /// <inheritdoc />
        public async Task<DateTimeOffset?> GetExpirationAsync(string fileId, CancellationToken _)
        {
            var expiration = _fileRepFactory
                .Expiration(await InternalFileId.Parse(_fileIdProvider, fileId))
                .ReadFirstLine(true);

            return expiration == null ? null : DateTimeOffset.ParseExact(expiration, "O", null);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<string>> GetExpiredFilesAsync(CancellationToken _)
        {
            var expiredFiles = new List<string>();
            foreach (var file in Directory.EnumerateFiles(_directoryPath, "*.expiration"))
            {
                var f = await InternalFileId.Parse(
                    _fileIdProvider,
                    Path.GetFileNameWithoutExtension(file)
                );
                if (FileHasExpired(f, _fileRepFactory) && FileIsIncomplete(f, _fileRepFactory))
                {
                    expiredFiles.Add(f);
                }
            }

            return expiredFiles;
        }

        /// <inheritdoc />
        public async Task<int> RemoveExpiredFilesAsync(CancellationToken _)
        {
            var expiredFiles = await GetExpiredFilesAsync(CancellationToken.None);
            var deleteFileTasks = expiredFiles
                .Select(file => DeleteFileAsync(file, CancellationToken.None))
                .ToList();

            await Task.WhenAll(deleteFileTasks);

            return deleteFileTasks.Count;
        }

        private static bool FileHasExpired(
            InternalFileId fileId,
            InternalFileRep.FileRepFactory fileRepFactory
        )
        {
            var firstLine = fileRepFactory.Expiration(fileId).ReadFirstLine();
            return !string.IsNullOrWhiteSpace(firstLine)
                && DateTimeOffset.ParseExact(firstLine, "O", null).HasPassed();
        }

        private static bool FileIsIncomplete(
            InternalFileId fileId,
            InternalFileRep.FileRepFactory fileRepFactory
        )
        {
            var uploadLength = fileRepFactory
                .UploadLength(fileId)
                .ReadFirstLineAsLong(fileIsOptional: true, defaultValue: long.MinValue);

            if (uploadLength == long.MinValue)
            {
                return true;
            }

            var dataFile = fileRepFactory.Data(fileId);

            if (!dataFile.Exist())
            {
                return true;
            }

            return uploadLength != dataFile.GetLength();
        }
    }
}
