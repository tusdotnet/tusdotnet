using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Expiration;

namespace AspNetCore_netcoreapp3_1_TestApp.Services
{
    public class ExpiredFilesCleanupService : BackgroundService
    {
        private readonly ITusExpirationStore _expirationStore;
        private readonly ExpirationBase _expiration;
        private readonly ILogger<ExpiredFilesCleanupService> _logger;

        public ExpiredFilesCleanupService(
            ILogger<ExpiredFilesCleanupService> logger,
            DefaultTusConfiguration config
        )
        {
            _logger = logger;
            _expirationStore = (ITusExpirationStore)config.Store;
            _expiration = config.Expiration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_expiration == null)
            {
                _logger.LogInformation("Not running cleanup job as no expiration has been set.");
                return;
            }

            await RunCleanup(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_expiration.Timeout, stoppingToken);
                await RunCleanup(stoppingToken);
            }
        }

        private async Task RunCleanup(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Running cleanup job...");
                var numberOfRemovedFiles = await _expirationStore.RemoveExpiredFilesAsync(
                    cancellationToken
                );
                _logger.LogInformation(
                    $"Removed {numberOfRemovedFiles} expired files. Scheduled to run again in {_expiration.Timeout.TotalMilliseconds} ms"
                );
            }
            catch (Exception exc)
            {
                _logger.LogWarning("Failed to run cleanup job: " + exc.Message);
            }
        }
    }
}
