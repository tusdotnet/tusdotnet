using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;

namespace tusdotnet.benchmark
{
    public class InMemoryStore : ITusStore, ITusCreationStore, ITusTerminationStore
    {
        public Dictionary<string, Stream> Data { get; set; }

        public Dictionary<string, string> Metadata { get; set; }

        public Dictionary<string, long> UploadLength { get; set; }

        public InMemoryStore()
        {
            Data = new Dictionary<string, Stream>();
            Metadata = new Dictionary<string, string>();
            UploadLength = new Dictionary<string, long>();
        }

        public Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken)
        {
            stream.CopyTo(Data[fileId]);
            return Task.FromResult(stream.Length);
        }

        public Task<string> CreateFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
        {
            var fileId = Guid.NewGuid().ToString();

            Data.Add(fileId, new MemoryStream());
            Metadata.Add(fileId, metadata);
            UploadLength.Add(fileId, uploadLength);

            return Task.FromResult(fileId);
        }

        public Task<bool> FileExistAsync(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Data.ContainsKey(fileId));
        }

        public Task<long?> GetUploadLengthAsync(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult((long?)UploadLength[fileId]);
        }

        public Task<string> GetUploadMetadataAsync(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Metadata[fileId]);
        }

        public Task<long> GetUploadOffsetAsync(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Data[fileId].Length);
        }

        public Task DeleteFileAsync(string fileId, CancellationToken cancellationToken)
        {
            //Data.Remove(fileId);
            //Metadata.Remove(fileId);
            //UploadLength.Remove(fileId);

            return Task.CompletedTask;
        }
    }
}
