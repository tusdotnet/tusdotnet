using System;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Expiration;

namespace tusdotnet.Helpers
{
    internal class ExpirationHelper
    {
        private readonly StoreAdapter _storeAdapter;
        private readonly ExpirationBase _expiration;
        private readonly bool _isSupported;
        private readonly Func<DateTimeOffset> _getSystemTime;

        public bool IsSlidingExpiration => _expiration is SlidingExpiration;

        internal ExpirationHelper(ContextAdapter context)
        {
            _storeAdapter = context.StoreAdapter;
            _expiration = context.Configuration.Expiration;
            _isSupported = _storeAdapter.Extensions.Expiration && _expiration != null;
            _getSystemTime = context.Configuration.GetSystemTime;
        }

        internal async Task<DateTimeOffset?> SetExpirationIfSupported(string fileId, CancellationToken cancellationToken)
        {
            if (!_isSupported)
            {
                return null;
            }

            var expires = _getSystemTime().Add(_expiration.Timeout);
            await _storeAdapter.SetExpirationAsync(fileId, expires, cancellationToken);

            return expires;
        }

        internal Task<DateTimeOffset?> GetExpirationIfSupported(string fileId, CancellationToken cancellationToken)
        {
            if (!_isSupported)
            {
                return Task.FromResult<DateTimeOffset?>(null);
            }

            return _storeAdapter.GetExpirationAsync(fileId, cancellationToken);
        }

        internal static string FormatHeader(DateTimeOffset? expires)
        {
            return expires?.ToString("R");
        }
    }
}