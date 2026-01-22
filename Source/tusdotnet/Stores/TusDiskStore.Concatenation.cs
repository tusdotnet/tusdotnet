using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;

namespace tusdotnet.Stores
{
    public partial class TusDiskStore
    {
        /// <inheritdoc />
        public async Task<FileConcat> GetUploadConcatAsync(string fileId, CancellationToken _)
        {
            var firstLine = _fileRepFactory
                .UploadConcat(await InternalFileId.Parse(_fileIdProvider, fileId))
                .ReadFirstLine(true);
            return string.IsNullOrWhiteSpace(firstLine) ? null : new UploadConcat(firstLine).Type;
        }

        /// <inheritdoc />
        public async Task<string> CreatePartialFileAsync(
            long uploadLength,
            string metadata,
            CancellationToken _
        )
        {
            var fileId = await CreateFileAsync(uploadLength, metadata, CancellationToken.None);
            _fileRepFactory
                .UploadConcat(await InternalFileId.Parse(_fileIdProvider, fileId))
                .Write(new FileConcatPartial().GetHeader());
            return fileId;
        }

        /// <inheritdoc />
        public async Task<string> CreateFinalFileAsync(
            string[] partialFiles,
            string metadata,
            CancellationToken _
        )
        {
            var partialInternalFileReps = await ValidateAndGetPartialFiles(partialFiles);
            var length = partialInternalFileReps.Sum(f => f.GetLength());
            var fileId = await CreateFileAsync(length, metadata, CancellationToken.None);
            var internalFileId = await InternalFileId.Parse(_fileIdProvider, fileId);

            await WriteUploadConcatHeader(internalFileId, partialFiles);
            await ConcatenatePartialFiles(internalFileId, partialInternalFileReps);
            await DeletePartialFilesIfConfigured(partialInternalFileReps);

            return fileId;
        }

        private async Task<InternalFileRep[]> ValidateAndGetPartialFiles(string[] partialFiles)
        {
            InternalFileRep[] partialInternalFileReps = new InternalFileRep[partialFiles.Length];
            for (int i = 0; i < partialFiles.Length; i++)
            {
                partialInternalFileReps[i] = _fileRepFactory.Data(
                    await InternalFileId.Parse(_fileIdProvider, partialFiles[i])
                );

                if (!partialInternalFileReps[i].Exist())
                {
                    throw new TusStoreException($"File {partialFiles[i]} does not exist");
                }
            }

            return partialInternalFileReps;
        }

        private async Task WriteUploadConcatHeader(
            InternalFileId internalFileId,
            string[] partialFiles
        )
        {
            _fileRepFactory
                .UploadConcat(internalFileId)
                .Write(new FileConcatFinal(partialFiles).GetHeader());
        }

        private async Task ConcatenatePartialFiles(
            InternalFileId internalFileId,
            InternalFileRep[] partialInternalFileReps
        )
        {
            using var finalFile = _fileRepFactory
                .Data(internalFileId)
                .GetStream(
                    System.IO.FileMode.Open,
                    System.IO.FileAccess.Write,
                    System.IO.FileShare.None
                );

            foreach (var partialFile in partialInternalFileReps)
            {
                using var partialStream = partialFile.GetStream(
                    System.IO.FileMode.Open,
                    System.IO.FileAccess.Read,
                    System.IO.FileShare.Read
                );

                await partialStream.CopyToAsync(finalFile);
            }
        }

        private async Task DeletePartialFilesIfConfigured(InternalFileRep[] partialInternalFileReps)
        {
            if (_deletePartialFilesOnConcat)
            {
                await Task.WhenAll(
                    partialInternalFileReps.Select(f =>
                        DeleteFileAsync(f.FileId, CancellationToken.None)
                    )
                );
            }
        }
    }
}
