using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;
using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace Owin_net452_TestApp.Extensions
{
    public static class CleanupJobIAppBuilderExtensions
    {
        public static void StartCleanupJob(
            this IAppBuilder app,
            DefaultTusConfiguration tusConfiguration
        )
        {
            var expiration = tusConfiguration.Expiration;

            if (expiration == null)
            {
                Console.WriteLine("Not running cleanup job as no expiration has been set.");
                return;
            }

            var expirationStore = (ITusExpirationStore)tusConfiguration.Store;
            var onAppDisposingToken = new OwinContext(app.Properties).Get<CancellationToken>(
                "host.OnAppDisposing"
            );
            Task.Run(
                async () =>
                {
                    while (!onAppDisposingToken.IsCancellationRequested)
                    {
                        Console.WriteLine("Running cleanup job...");

                        var numberOfRemovedFiles = await expirationStore.RemoveExpiredFilesAsync(
                            onAppDisposingToken
                        );

                        Console.WriteLine(
                            $"Removed {numberOfRemovedFiles} expired files. Scheduled to run again in {expiration.Timeout.TotalMilliseconds} ms"
                        );

                        await Task.Delay(expiration.Timeout, onAppDisposingToken);
                    }
                },
                onAppDisposingToken
            );
        }
    }
}
