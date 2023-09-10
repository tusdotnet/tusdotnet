using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Helpers;

namespace tusdotnet.Models
{
    internal class ClientDisconnectGuardedReadOnlyStream : ReadOnlyStream
    {
        private readonly ClientDisconnectGuardWithTimeout _clientDisconnectGuard;

        internal ClientDisconnectGuardedReadOnlyStream(Stream backingStream, ClientDisconnectGuardWithTimeout clientDisconnectGuard)
            : base(backingStream)
        {
            _clientDisconnectGuard = clientDisconnectGuard;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var result = await _clientDisconnectGuard.Execute(
                guardFromClientDisconnect: async () =>
                {
                    var bytesRead = await BackingStream.ReadAsync(buffer, offset, count, cancellationToken);
                    return new ClientDisconnectGuardReadStreamAsyncResult(false, bytesRead);
                },
                getDefaultValue: () => new ClientDisconnectGuardReadStreamAsyncResult(true, 0),
                cancellationToken);

            return result.BytesRead;
        }
    }
}