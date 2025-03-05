using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Expiration;

namespace AspNetCore_netcoreapp2_1_TestApp.Services
{
    public class ExpiredFilesCleanupService : IHostedService
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

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_expiration == null)
            {
                _logger.LogInformation("Not running cleanup job as no expiration has been set.");
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                await RunCleanup(cancellationToken);
                await Task.Delay(_expiration.Timeout);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task RunCleanup(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Running cleanup job...");
            var numberOfRemovedFiles = await _expirationStore.RemoveExpiredFilesAsync(
                cancellationToken
            );
            _logger.LogInformation(
                $"Removed {numberOfRemovedFiles} expired files. Scheduled to run again in {_expiration.Timeout.TotalMilliseconds} ms"
            );
        }
    }
}
