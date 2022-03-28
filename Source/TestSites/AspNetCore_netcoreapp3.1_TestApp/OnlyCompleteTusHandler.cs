using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using tusdotnet.Tus2;

namespace AspNetCore_netcoreapp3._1_TestApp
{
    public class OnlyCompleteTusHandler : TusHandler
    {
        private readonly ILogger<OnlyCompleteTusHandler> _logger;

        public OnlyCompleteTusHandler(ILoggerFactory loggerFactory, ITus2ConfigurationManager config)
            : base(config, "MyStorage")
        {
            _logger = loggerFactory.CreateLogger<OnlyCompleteTusHandler>();
        }

        public override Task FileComplete(FileCompleteContext context)
        {
            _logger.LogInformation("File completed: {FileName}", context.Headers.UploadToken);
            return base.FileComplete(context);
        }
    }
}
