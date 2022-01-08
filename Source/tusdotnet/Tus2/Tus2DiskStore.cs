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
    public class Tus2DiskStore
    {

        private static readonly HashSet<char> _invalidFileNameChars = new(Path.GetInvalidFileNameChars());

        private readonly Tus2Options _options;

        public Tus2DiskStore(Tus2Options options)
        {
            _options = options;
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

        internal Task<bool> FileExist(string uploadToken)
        {
            uploadToken = CleanUploadToken(uploadToken);
            return Task.FromResult(File.Exists(_options.DataFilePath(uploadToken)));
        }

        internal Task<long> GetOffset(string uploadToken)
        {
            uploadToken = CleanUploadToken(uploadToken);
            return Task.FromResult(new FileInfo(_options.DataFilePath(uploadToken)).Length);
        }

        internal Task CreateFile(string uploadToken, CreateFileOptions options = null)
        {
            uploadToken = CleanUploadToken(uploadToken);

            if(options?.Metadata != null)
                File.WriteAllText(_options.MetadataFilePath(uploadToken), GetMetadataString(options.Metadata));

            File.Create(_options.DataFilePath(uploadToken)).Dispose();
            return Task.CompletedTask;
        }

        internal async Task AppendData(string uploadToken, PipeReader reader, CancellationToken cancellationToken, WriteFileOptions options = null)
        {
            uploadToken = CleanUploadToken(uploadToken);
            const int JUST_BELOW_LOH_BYTE_LIMIT = 84 * 1024;

            var dataFilePath = _options.DataFilePath(uploadToken);

            if (options?.Metadata != null)
                File.WriteAllText(_options.MetadataFilePath(uploadToken), GetMetadataString(options.Metadata));


            using var diskFileStream = new FileStream(dataFilePath, FileMode.Append, FileAccess.Write, FileShare.None, bufferSize: JUST_BELOW_LOH_BYTE_LIMIT);

            ReadResult result = default;

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

        internal Task MarkComplete(string uploadToken)
        {
            uploadToken = CleanUploadToken(uploadToken);
            File.Create(_options.CompletedFilePath(uploadToken)).Dispose();
            return Task.CompletedTask;
        }

        internal Task<bool> IsComplete(string uploadToken)
        {
            uploadToken = CleanUploadToken(uploadToken);
            return Task.FromResult(File.Exists(_options.CompletedFilePath(uploadToken)));
        }

        internal Task Delete(string uploadToken)
        {
            uploadToken = CleanUploadToken(uploadToken);
            File.Delete(_options.CompletedFilePath(uploadToken));
            File.Delete(_options.DataFilePath(uploadToken));
            File.Delete(_options.MetadataFilePath(uploadToken));
            return Task.CompletedTask;
        }

        private static string GetMetadataString(IDictionary<string, Metadata> metadata)
        {
            return System.Text.Json.JsonSerializer.Serialize(metadata.ToDictionary(x => x.Key, x => Convert.ToBase64String(x.Value.GetBytes())));
        }
    }
}