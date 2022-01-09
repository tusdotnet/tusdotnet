using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Models;

namespace tusdotnet.Tus2
{
    // TODO: Use RandomAccess if possible
    // TODO: Support for partitioning
    public class Tus2DiskStore : ITus2Store
    {

        private static readonly HashSet<char> _invalidFileNameChars = new(Path.GetInvalidFileNameChars());

        private readonly DiskPathHelper _diskPathHelper;

        public Tus2DiskStore(string diskPath)
        {
            _diskPathHelper = new DiskPathHelper(diskPath);
        }

        public static string CleanUploadToken(string uploadToken)
        {
            var result = uploadToken;
            // TODO: This should not be here but in the disk store

            var span = result.ToCharArray();

            for (int i = 0; i < span.Length; i++)
            {
                if (_invalidFileNameChars.Contains(span[i]))
                    span[i] = '_';
            }

            return new string(span);
        }

        public Task<bool> FileExist(string uploadToken)
        {
            uploadToken = CleanUploadToken(uploadToken);
            return Task.FromResult(File.Exists(_diskPathHelper.DataFilePath(uploadToken)));
        }

        public Task<long> GetOffset(string uploadToken)
        {
            uploadToken = CleanUploadToken(uploadToken);
            return Task.FromResult(new FileInfo(_diskPathHelper.DataFilePath(uploadToken)).Length);
        }

        public Task CreateFile(string uploadToken, CreateFileContext options = null)
        {
            uploadToken = CleanUploadToken(uploadToken);

            if (options?.Metadata != null)
                File.WriteAllText(_diskPathHelper.MetadataFilePath(uploadToken), GetMetadataString(options.Metadata));

            File.Create(_diskPathHelper.DataFilePath(uploadToken)).Dispose();
            return Task.CompletedTask;
        }

        public async Task WriteData(string uploadToken, WriteDataContext options)
        {
            uploadToken = CleanUploadToken(uploadToken);
            const int JUST_BELOW_LOH_BYTE_LIMIT = 84 * 1024;

            var dataFilePath = _diskPathHelper.DataFilePath(uploadToken);

            if (options?.Metadata != null)
                File.WriteAllText(_diskPathHelper.MetadataFilePath(uploadToken), GetMetadataString(options.Metadata));


            using var diskFileStream = new FileStream(dataFilePath, FileMode.Append, FileAccess.Write, FileShare.None, bufferSize: JUST_BELOW_LOH_BYTE_LIMIT);

            ReadResult result = default;
            var cancellationToken = options.CancellationToken;
            var reader = options.BodyReader;

            try
            {
                while (!PipeReadingIsDone(result, cancellationToken))
                {
                    result = await reader.ReadAsync(cancellationToken);

                    foreach (var segment in result.Buffer)
                    {
                        await diskFileStream.WriteAsync(segment, CancellationToken.None);
                    }

                    reader.AdvanceTo(result.Buffer.End);
                }

                await reader.CompleteAsync();
                await diskFileStream.FlushAsync(CancellationToken.None);
            }
            catch (Exception)
            {
                // Clear memory and complete the reader to not cause a Microsoft.AspNetCore.Connections.ConnectionAbortedException inside Kestrel later on as this is an "expected" exception.
                reader.AdvanceTo(result.Buffer.End);
                await reader.CompleteAsync();
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool PipeReadingIsDone(ReadResult result, CancellationToken cancellationToken)
        {
            return cancellationToken.IsCancellationRequested || result.IsCanceled || result.IsCompleted;
        }

        public Task MarkComplete(string uploadToken)
        {
            uploadToken = CleanUploadToken(uploadToken);
            File.Create(_diskPathHelper.CompletedFilePath(uploadToken)).Dispose();
            return Task.CompletedTask;
        }

        public Task<bool> IsComplete(string uploadToken)
        {
            uploadToken = CleanUploadToken(uploadToken);
            return Task.FromResult(File.Exists(_diskPathHelper.CompletedFilePath(uploadToken)));
        }

        public Task Delete(string uploadToken)
        {
            uploadToken = CleanUploadToken(uploadToken);
            File.Delete(_diskPathHelper.CompletedFilePath(uploadToken));
            File.Delete(_diskPathHelper.DataFilePath(uploadToken));
            File.Delete(_diskPathHelper.MetadataFilePath(uploadToken));
            return Task.CompletedTask;
        }

        private static string GetMetadataString(IDictionary<string, Metadata> metadata)
        {
            return System.Text.Json.JsonSerializer.Serialize(metadata.ToDictionary(x => x.Key, x => Convert.ToBase64String(x.Value.GetBytes())));
        }
    }
}