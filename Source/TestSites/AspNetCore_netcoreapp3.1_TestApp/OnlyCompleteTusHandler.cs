using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using tusdotnet.Tus2;

namespace AspNetCore_netcoreapp3._1_TestApp
{
    public class OnlyCompleteTusHandler : TusHandler
    {
        private readonly ILogger<OnlyCompleteTusHandler> _logger;

        public OnlyCompleteTusHandler(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<OnlyCompleteTusHandler>();
        }

        public override Task OnFileComplete()
        {
            _logger.LogInformation("File completed: {FileName}", base.TusContext.Headers.UploadToken);
            return base.OnFileComplete();
        }
    }
}
