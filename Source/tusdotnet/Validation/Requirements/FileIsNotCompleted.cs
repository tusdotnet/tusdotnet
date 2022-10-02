﻿using System.Threading.Tasks;
using tusdotnet.Adapters;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class FileIsNotCompleted : Requirement
    {
        public override async Task Validate(ContextAdapter context)
        {
            var fileId = context.FileId;

            var fileUploadLength = context.StoreAdapter.GetUploadLengthAsync(fileId, context.CancellationToken);
            var fileOffset = context.StoreAdapter.Store.GetUploadOffsetAsync(fileId, context.CancellationToken);

            await Task.WhenAll(fileUploadLength, fileOffset);

            if (fileUploadLength != null && fileOffset.Result == fileUploadLength.Result)
            {
                await BadRequest("Upload is already complete.");
            }
        }
    }
}
