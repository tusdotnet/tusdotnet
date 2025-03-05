using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Expiration;

namespace AspNetCore_net462_TestApp.Services
{
    public class ExpiredFilesCleanupService
    {
        private readonly ITusExpirationStore _expirationStore;
        private readonly ExpirationBase _expiration;
        private readonly ILogger<ExpiredFilesCleanupService> _logger;
        private readonly IApplicationLifetime _applicationLifetime;

        public ExpiredFilesCleanupService(
            IApplicationLifetime applicationLifetime,
            DefaultTusConfiguration tusConfiguration,
            ILoggerFactory loggerFactory
        )
        {
            _applicationLifetime = applicationLifetime;
            _expirationStore = (ITusExpirationStore)tusConfiguration.Store;
            _expiration = tusConfiguration.Expiration;
            _logger = loggerFactory.CreateLogger<ExpiredFilesCleanupService>();
        }

        public void Start()
        {
            if (_expiration == null)
            {
                _logger.LogInformation("Not running cleanup job as no expiration has been set.");
                return;
            }

            var onAppDisposingToken = _applicationLifetime.ApplicationStopping;
            Task.Run(
                async () =>
                {
                    while (!onAppDisposingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Running cleanup job...");

                        var numberOfRemovedFiles = await _expirationStore.RemoveExpiredFilesAsync(
                            onAppDisposingToken
                        );

                        _logger.LogInformation(
                            $"Removed {numberOfRemovedFiles} expired files. Scheduled to run again in {_expiration.Timeout.TotalMilliseconds} ms"
                        );

                        await Task.Delay(_expiration.Timeout, onAppDisposingToken);
                    }
                },
                onAppDisposingToken
            );
        }
    }
}
