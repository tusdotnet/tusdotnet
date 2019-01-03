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