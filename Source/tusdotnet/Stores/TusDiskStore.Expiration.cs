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
            await _fileRepFactory
                .Expiration(await InternalFileId.Parse(_fileIdProvider, fileId))
                .WriteAsync(expires.ToString("O"));
        }

        /// <inheritdoc />
        public async Task<DateTimeOffset?> GetExpirationAsync(
            string fileId,
            CancellationToken cancellationToken
        )
        {
            var expiration = await _fileRepFactory
                .Expiration(await InternalFileId.Parse(_fileIdProvider, fileId))
                .ReadTextAsync(true, cancellationToken);

            return expiration == null ? null : DateTimeOffset.ParseExact(expiration, "O", null);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<string>> GetExpiredFilesAsync(
            CancellationToken cancellationToken
        )
        {
            var expiredFiles = new List<string>();
            try
            {
                foreach (var file in Directory.EnumerateFiles(_directoryPath, "*.expiration"))
                {
                    var f = await InternalFileId.Parse(
                        _fileIdProvider,
                        Path.GetFileNameWithoutExtension(file)
                    );
                    if (
                        await FileHasExpired(f, _fileRepFactory, cancellationToken)
                        && await FileIsIncomplete(f, _fileRepFactory, cancellationToken)
                    )
                    {
                        expiredFiles.Add(f);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // As this method is most likely called as a cleanup job just return what we've found so far rather than throwing.
            }

            return expiredFiles;
        }

        /// <inheritdoc />
        public async Task<int> RemoveExpiredFilesAsync(CancellationToken cancellationToken)
        {
            var expiredFiles = await GetExpiredFilesAsync(cancellationToken);
            var deleteFileTasks = expiredFiles
                .Select(file => DeleteFileAsync(file, CancellationToken.None))
                .ToList();

            await Task.WhenAll(deleteFileTasks);

            return deleteFileTasks.Count;
        }

        private static async Task<bool> FileHasExpired(
            InternalFileId fileId,
            InternalFileRep.FileRepFactory fileRepFactory,
            CancellationToken cancellationToken
        )
        {
            var firstLine = await fileRepFactory
                .Expiration(fileId)
                .ReadTextAsync(fileIsOptional: false, cancellationToken);
            return !string.IsNullOrWhiteSpace(firstLine)
                && DateTimeOffset.ParseExact(firstLine, "O", null).HasPassed();
        }

        private static async Task<bool> FileIsIncomplete(
            InternalFileId fileId,
            InternalFileRep.FileRepFactory fileRepFactory,
            CancellationToken cancellationToken
        )
        {
            var uploadLength = await fileRepFactory
                .UploadLength(fileId)
                .ReadTextAsLongAsync(
                    fileIsOptional: true,
                    defaultValue: long.MinValue,
                    cancellationToken
                );

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
