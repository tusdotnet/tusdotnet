using System.Runtime.CompilerServices;
using System.Threading;

namespace tusdotnet.Helpers
{
    /// <summary>
    /// Maps the Stream/PipeReader passed to the store to the request's lock-lost token, so the token does not
    /// have to pass through every guard that wraps the request body. Locks that cannot be lost register nothing.
    /// </summary>
    internal static class FileLockLostTokenRegistry
    {
        private static readonly ConditionalWeakTable<object, TokenHolder> _tokens = new();

        internal static void Register(object requestBody, CancellationToken lockLostToken)
        {
            if (!lockLostToken.CanBeCanceled)
                return;

            _tokens.Add(requestBody, new TokenHolder(lockLostToken));
        }

        internal static CancellationToken Get(object requestBody)
        {
            return requestBody is not null && _tokens.TryGetValue(requestBody, out var holder)
                ? holder.Token
                : CancellationToken.None;
        }

        private sealed class TokenHolder
        {
            public CancellationToken Token { get; }

            public TokenHolder(CancellationToken token)
            {
                Token = token;
            }
        }
    }
}
