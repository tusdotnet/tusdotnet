using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.IntentHandlers;
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

            if (await HandleOnBeforeDeleteAsync(Context))
            {
                return ResultType.Handled;
            }

            await _terminationStore.DeleteFileAsync(Context.RequestFileId, cancellationToken);

            await HandleOnDeleteCompleteAsync(Context);

            response.SetStatus((int)HttpStatusCode.NoContent);
            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);

            return ResultType.Handled;
        }

        private static Task<bool> HandleOnBeforeDeleteAsync(ContextAdapter context)
        {
            if (context.Configuration.Events?.OnBeforeDeleteAsync == null)
            {
                return Task.FromResult(false);
            }

            return HandleOnBeforeDeleteAsyncLocal();

            async Task<bool> HandleOnBeforeDeleteAsyncLocal()
            {
                var beforeDeleteContext = BeforeDeleteContext.Create(context);
                await context.Configuration.Events.OnBeforeDeleteAsync(beforeDeleteContext);
                if (beforeDeleteContext.HasFailed)
                {
                    await context.Response.Error(HttpStatusCode.BadRequest, beforeDeleteContext.ErrorMessage);
                    return true;
                }

                return false;
            }
        }

        private static Task HandleOnDeleteCompleteAsync(ContextAdapter context)
        {
            if (context.Configuration.Events?.OnDeleteCompleteAsync == null)
            {
                return Task.FromResult(0);
            }

            return context.Configuration.Events.OnDeleteCompleteAsync(DeleteCompleteContext.Create(context));
        }
    }
}