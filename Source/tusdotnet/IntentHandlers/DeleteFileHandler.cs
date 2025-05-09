﻿using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Models;
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
        public DeleteFileHandler(ContextAdapter context)
            : base(context, IntentType.DeleteFile, LockType.RequiresLock) { }

        internal override Requirement[] Requires =>
            new Requirement[]
            {
                new Validation.Requirements.FileExist(),
                new Validation.Requirements.FileHasNotExpired(),
            };

        internal override async Task Invoke()
        {
            await StoreAdapter.DeleteFileAsync(Context.FileId, CancellationToken);

            Response.SetResponse(HttpStatusCode.NoContent);
            Response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
        }
    }
}
