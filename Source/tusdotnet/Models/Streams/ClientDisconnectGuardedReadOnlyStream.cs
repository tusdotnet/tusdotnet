using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Helpers;

namespace tusdotnet.Models
{
    internal class ClientDisconnectGuardedReadOnlyStream : ReadOnlyStream
    {
        internal CancellationToken CancellationToken { get; }

        private readonly CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Default ctor
        /// </summary>
        /// <param name="backingStream">The stream to guard against client disconnects</param>
        /// <param name="cancellationTokenSource">Token source to cancel when the client disconnects. Preferably use CancellationTokenSource.CreateLinkedTokenSource(RequestCancellationToken).</param>
        internal ClientDisconnectGuardedReadOnlyStream(Stream backingStream, CancellationTokenSource cancellationTokenSource)
            : base(backingStream)
        {
            CancellationToken = cancellationTokenSource.Token;

            _cancellationTokenSource = cancellationTokenSource;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var result = await ClientDisconnectGuard.ReadStreamAsync(BackingStream, buffer, offset, count, cancellationToken);

            if (result.ClientDisconnected)
            {
                _cancellationTokenSource.Cancel();
                return 0;
            }

            return result.BytesRead;
        }
    }
}