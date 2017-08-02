using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;
using tusdotnet.Validation;
using tusdotnet.Validation.Requirements;

namespace tusdotnet.ProtocolHandlers
{
    /* 
     * When receiving a DELETE request for an existing upload the Server SHOULD free associated resources and MUST 
     * respond with the 204 No Content status confirming that the upload was terminated. 
     * For all future requests to this URL the Server SHOULD respond with the 404 Not Found or 410 Gone status.
    */
    internal class DeleteHandler : ProtocolMethodHandler
    {
        internal override bool RequiresLock => true;

        internal override Requirement[] Requires => new Requirement[]
        {
            new FileExist(),
            new FileHasNotExpired()
        };

        internal override bool CanHandleRequest(ContextAdapter context)
        {
            return context.Configuration.Store is ITusTerminationStore && context.UrlMatchesFileIdUrl();
        }

        internal override async Task<bool> Handle(ContextAdapter context)
        {
            var response = context.Response;
            var cancellationToken = context.CancellationToken;
            var store = context.Configuration.Store;

            var fileId = context.GetFileId();

            await ((ITusTerminationStore)store).DeleteFileAsync(fileId, cancellationToken);
            response.SetStatus((int)HttpStatusCode.NoContent);
            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);

            return true;
        }
    }
}
