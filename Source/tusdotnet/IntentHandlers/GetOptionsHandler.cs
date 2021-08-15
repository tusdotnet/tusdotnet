using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Validation;

namespace tusdotnet.IntentHandlers
{
    /*
    * An OPTIONS request MAY be used to gather information about the Server’s current configuration. 
    * A successful response indicated by the 204 No Content status MUST contain the Tus-Version header. 
    * It MAY include the Tus-Extension and Tus-Max-Size headers.
    * The Client SHOULD NOT include the Tus-Resumable header in the request and the Server MUST discard it.
    */

    internal class GetOptionsHandler : IntentHandler
    {
        private readonly ChecksumHelper _checksumHelper;

        internal override Requirement[] Requires => NoRequirements;

        public GetOptionsHandler(ContextAdapter context)
            : base(context, IntentType.GetOptions, LockType.NoLock)
        {
            _checksumHelper = new ChecksumHelper(context);
        }

        internal override async Task Invoke()
        {
            var response = Context.Response;

            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetHeader(HeaderConstants.TusVersion, HeaderConstants.TusResumableValue);

            var maximumAllowedSize = Context.Configuration.GetMaxAllowedUploadSizeInBytes();

            if (maximumAllowedSize.HasValue)
            {
                response.SetHeader(HeaderConstants.TusMaxSize, maximumAllowedSize.Value.ToString());
            }

            var extensions = DetectExtensions();
            if (extensions.Count > 0)
            {
                response.SetHeader(HeaderConstants.TusExtension, string.Join(",", extensions));
            }

            if (Context.Configuration.Store is ITusChecksumStore checksumStore)
            {
                var checksumAlgorithms = await checksumStore.GetSupportedAlgorithmsAsync(Context.CancellationToken);
                response.SetHeader(HeaderConstants.TusChecksumAlgorithm, string.Join(",", checksumAlgorithms));
            }

            response.SetStatus(HttpStatusCode.NoContent);
        }

        private List<string> DetectExtensions()
        {
            var extensions = new List<string>(7);

            if (Context.Configuration.Store is ITusCreationStore)
            {
                extensions.Add(ExtensionConstants.Creation);
                extensions.Add(ExtensionConstants.CreationWithUpload);
            }

            if (Context.Configuration.Store is ITusTerminationStore)
            {
                extensions.Add(ExtensionConstants.Termination);
            }

            if (Context.Configuration.Store is ITusChecksumStore)
            {
                extensions.Add(ExtensionConstants.Checksum);

                if (_checksumHelper.SupportsChecksumTrailer())
                {
                    extensions.Add(ExtensionConstants.ChecksumTrailer);
                }
            }

            if (Context.Configuration.Store is ITusConcatenationStore)
            {
                extensions.Add(ExtensionConstants.Concatenation);
            }

            if (Context.Configuration.Store is ITusExpirationStore)
            {
                extensions.Add(ExtensionConstants.Expiration);
            }

            if (Context.Configuration.Store is ITusCreationDeferLengthStore)
            {
                extensions.Add(ExtensionConstants.CreationDeferLength);
            }

            return extensions;
        }
    }
}
