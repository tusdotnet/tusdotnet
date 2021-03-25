using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.ExternalMiddleware.EndpointRouting;
using tusdotnet.Models;

namespace AspNetCore_netcoreapp3._1_TestApp
{
    public class MyTusController : TusController<MyTusConfigurator>
    {
        private readonly ILogger<MyTusController> _logger;

        public MyTusController(StorageService<MyTusConfigurator> storage, ILogger<MyTusController> logger)
            : base(storage)
        {
            _logger = logger;
        }

        [Authorize(Policy = "create-file-policy")]
        public override async Task<IActionResult> Create(CreateContext context, CancellationToken cancellation)
        {
            var errors = ValidateMetadata(context.Metadata);

            if (errors.Count > 0)
            {
                return new BadRequestObjectResult(errors);
            }

            var result = await base.Create(context, cancellation).ConfigureAwait(false);

            _logger.LogInformation($"File created with id {context.FileId}");

            return result;
        }

        private List<string> ValidateMetadata(IDictionary<string, Metadata> metadata)
        {
            var errors = new List<string>();

            if (!metadata.ContainsKey("name") || metadata["name"].HasEmptyValue)
            {
                errors.Add("name metadata must be specified.");
            }

            if (!metadata.ContainsKey("contentType") || metadata["contentType"].HasEmptyValue)
            {
                errors.Add("contentType metadata must be specified.");
            }

            return errors;
        }

        public override async Task<IActionResult> Write(WriteContext context, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Started writing file {context.FileId} at offset {context.UploadOffset}");

            var result = await base.Write(context, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation($"Done writing file {context.FileId}. New offset: {context.UploadOffset}");

            return result;
        }

        public override Task<IActionResult> FileCompleted(FileCompletedContext context, CancellationToken cancellation)
        {
            _logger.LogInformation($"Upload of file {context.FileId} is complete!");
            return base.FileCompleted(context, cancellation);
        }
    }
}
