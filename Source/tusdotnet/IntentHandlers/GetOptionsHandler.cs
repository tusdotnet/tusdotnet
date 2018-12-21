using System;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Validation;

namespace tusdotnet.IntentHandlers
{
    internal class GetOptionsHandler : IntentHandler
    {
        internal override bool RequiresLock => false;

        internal override IntentType Intent => IntentType.GetOptions;

        internal override Requirement[] Requires => new Requirement[0];

        public GetOptionsHandler(ContextAdapter context) : base(context)
        {
            ShouldValidateTusResumableHeader = false;
        }

        internal override async Task<ResultType> Invoke()
        {
            var response = Context.Response;

            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetHeader(HeaderConstants.TusVersion, HeaderConstants.TusResumableValue);

            var maximumAllowedSize = Context.Configuration.GetMaxAllowedUploadSizeInBytes();

            if (maximumAllowedSize.HasValue)
            {
                response.SetHeader(HeaderConstants.TusMaxSize, maximumAllowedSize.Value.ToString());
            }

            var extensions = Context.DetectExtensions();
            if (extensions.Count > 0)
            {
                response.SetHeader(HeaderConstants.TusExtension, string.Join(",", extensions));
            }

            if (Context.Configuration.Store is ITusChecksumStore checksumStore)
            {
                var checksumAlgorithms = await checksumStore.GetSupportedAlgorithmsAsync(Context.CancellationToken);
                response.SetHeader(HeaderConstants.TusChecksumAlgorithm, string.Join(",", checksumAlgorithms));
            }

            response.SetStatus((int)HttpStatusCode.NoContent);

            return ResultType.Handled;
        }
    }
}
