using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Helpers;

namespace tusdotnet.Models
{
    internal class ClientDisconnectGuardedReadOnlyStream : ReadOnlyStream
    {
        private readonly ClientDisconnectGuardWithTimeout _clientDisconnectGuard;

        internal ClientDisconnectGuardedReadOnlyStream(
            Stream backingStream,
            ClientDisconnectGuardWithTimeout clientDisconnectGuard
        )
            : base(backingStream)
        {
            _clientDisconnectGuard = clientDisconnectGuard;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            var result = await _clientDisconnectGuard.ExecuteTask(
                state: new ReadState(BackingStream, buffer, offset, count),
                operation: static async (s, ct) =>
                {
                    var bytesRead = await s._stream.ReadAsync(s._buffer, s._offset, s._count, ct);
                    return new ClientDisconnectGuardReadStreamAsyncResult(false, bytesRead);
                },
                getDefaultValue: static _ => new ClientDisconnectGuardReadStreamAsyncResult(
                    true,
                    0
                ),
                guardedToken: cancellationToken
            );

            return result.BytesRead;
        }

        private readonly struct ReadState
        {
            internal readonly Stream _stream;
            internal readonly byte[] _buffer;
            internal readonly int _offset;
            internal readonly int _count;

            internal ReadState(Stream stream, byte[] buffer, int offset, int count)
            {
                _stream = stream;
                _buffer = buffer;
                _offset = offset;
                _count = count;
            }
        }
    }
}
