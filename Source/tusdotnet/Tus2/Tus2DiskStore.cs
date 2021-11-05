using System;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    // TODO: Use RandomAccess if possible
    // TODO: Support for partitioning
    internal class Tus2DiskStore
    {
        private readonly Tus2Options _options;

        public Tus2DiskStore(Tus2Options options)
        {
            _options = options;
        }

        internal Task<bool> FileExist(string uploadToken)
        {
            return Task.FromResult(File.Exists(_options.DataFilePath(uploadToken)));
        }

        internal Task<long> GetOffset(string uploadToken)
        {
            return Task.FromResult(new FileInfo(_options.DataFilePath(uploadToken)).Length);
        }

        internal Task CreateFile(string uploadToken)
        {
            File.Create(_options.DataFilePath(uploadToken)).Dispose();
            return Task.CompletedTask;
        }

        internal async Task AppendData(string uploadToken, PipeReader reader, CancellationToken cancellationToken)
        {
            const int JUST_BELOW_LOH_BYTE_LIMIT = 84 * 1024;

            var dataFilePath = _options.DataFilePath(uploadToken);

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
            File.Create(_options.CompletedFilePath(uploadToken)).Dispose();
            return Task.CompletedTask;
        }

        internal Task<bool> IsComplete(string uploadToken)
        {
            return Task.FromResult(File.Exists(_options.CompletedFilePath(uploadToken)));
        }

        internal Task Delete(string uploadToken)
        {
            File.Delete(_options.CompletedFilePath(uploadToken));
            File.Delete(_options.DataFilePath(uploadToken));
            return Task.CompletedTask;
        }
    }
}