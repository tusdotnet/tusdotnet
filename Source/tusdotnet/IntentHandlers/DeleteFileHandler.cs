using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Extensions.Internal;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Parsers;
using tusdotnet.Validation;

namespace tusdotnet.IntentHandlers
{
    /* 
    * When receiving a DELETE request for an existing upload the Server SHOULD free associated resources and MUST 
    * respond with the 204 No Content status confirming that the upload was terminated. 
    * For all future requests to this URL the Server SHOULD respond with the 404 Not Found or 410 Gone status.
    */

    internal class DeleteFileHandler : IntentHandler
    {
        private readonly ITusTerminationStore _terminationStore;

        public DeleteFileHandler(ContextAdapter context, ITusTerminationStore terminationStore)
            : base(context, IntentType.DeleteFile, LockType.RequiresLock)
        {
            _terminationStore = terminationStore;
        }

        internal override Requirement[] Requires => new Requirement[]
        {
            new Validation.Requirements.FileExist(),
            new Validation.Requirements.FileHasNotExpired()
        };

        internal override async Task<ResultType> Challenge(UploadChallengeParserResult uploadChallenge, ITusChallengeStoreHashFunction hashFunction, ITusChallengeStore challengeStore)
        {
            var secret = await challengeStore.GetUploadSecretAsync(Context.Request.FileId, Context.CancellationToken);

            if (string.IsNullOrEmpty(secret))
                return ResultType.ContinueExecution;

            if (!uploadChallenge.AssertUploadChallengeIsProvidedIfSecretIsSet(secret))
            {
                Context.Response.NotFound();
                return ResultType.StopExecution;
            }

            if (!uploadChallenge.VerifyChecksum(Context.Request.GetHeader(HeaderConstants.UploadOffset), Context.Request.GetHttpMethod(), secret, hashFunction))
            {
                Context.Response.NotFound();
                return ResultType.StopExecution;
            }

            return ResultType.ContinueExecution;
        }

        internal override async Task Invoke()
        {
            if (await EventHelper.Validate<BeforeDeleteContext>(Context) == ResultType.StopExecution)
            {
                return;
            }

            await _terminationStore.DeleteFileAsync(Request.FileId, CancellationToken);

            await EventHelper.Notify<DeleteCompleteContext>(Context);

            Response.SetStatus(HttpStatusCode.NoContent);
            Response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
        }
    }
}