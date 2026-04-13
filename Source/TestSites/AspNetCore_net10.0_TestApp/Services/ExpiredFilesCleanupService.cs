using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Expiration;

namespace AspNetCore_net10._0_TestApp.Services;

public sealed class ExpiredFilesCleanupService : BackgroundService
{
    private readonly ITusExpirationStore _expirationStore;
    private readonly ExpirationBase? _expiration;
    private readonly ILogger<ExpiredFilesCleanupService> _logger;

    public ExpiredFilesCleanupService(
        ILogger<ExpiredFilesCleanupService> logger,
        DefaultTusConfiguration config
    )
    {
        _logger = logger;
        _expirationStore = config.Store as ITusExpirationStore
            ?? throw new InvalidOperationException(
                $"The store {config.Store.GetType().Name} does not implement ITusExpirationStore.");
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

        using var timer = new PeriodicTimer(_expiration.Timeout);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunCleanup(stoppingToken);
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
                $"Removed {numberOfRemovedFiles} expired files. Scheduled to run again in {_expiration!.Timeout.TotalMilliseconds} ms"
            );
        }
        catch (Exception exc)
        {
            _logger.LogWarning("Failed to run cleanup job: " + exc.Message);
        }
    }
}
