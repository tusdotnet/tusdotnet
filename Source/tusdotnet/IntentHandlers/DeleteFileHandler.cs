using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Validation;

namespace tusdotnet.IntentHandlers
{
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

        internal override async Task<ResultType> Invoke()
        {
            var response = Context.Response;
            var cancellationToken = Context.CancellationToken;
            var store = Context.Configuration.Store;

            if (await EventHelper.Validate<BeforeDeleteContext>(Context) == ResultType.StopExecution)
            {
                return ResultType.StopExecution;
            }

            await _terminationStore.DeleteFileAsync(Context.RequestFileId, cancellationToken);

            await EventHelper.Notify<DeleteCompleteContext>(Context);

            response.SetStatus((int)HttpStatusCode.NoContent);
            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);

            return ResultType.StopExecution;
        }
    }
}