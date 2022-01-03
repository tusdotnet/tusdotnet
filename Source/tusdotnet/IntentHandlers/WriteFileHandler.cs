using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Helpers;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Validation;
using tusdotnet.Validation.Requirements;

namespace tusdotnet.IntentHandlers
{
    /*
    * The Server SHOULD accept PATCH requests against any upload URL and apply the bytes 
    * contained in the message at the given offset specified by the Upload-Offset header. 
    * All PATCH requests MUST use Content-Type: application/offset+octet-stream.
    * The Upload-Offset header’s value MUST be equal to the current offset of the resource. 
    * In order to achieve parallel upload the Concatenation extension MAY be used. 
    * If the offsets do not match, the Server MUST respond with the 409 Conflict status without modifying the upload resource.
    * The Client SHOULD send all the remaining bytes of an upload in a single PATCH request, 
    * but MAY also use multiple small requests successively for scenarios where 
    * this is desirable, for example, if the Checksum extension is used.
    * The Server MUST acknowledge successful PATCH requests with the 204 No Content status. 
    * It MUST include the Upload-Offset header containing the new offset. 
    * The new offset MUST be the sum of the offset before the PATCH request and the number of bytes received and 
    * processed or stored during the current PATCH request.
    * Both, Client and Server, SHOULD attempt to detect and handle network errors predictably. 
    * They MAY do so by checking for read/write socket errors, as well as setting read/write timeouts. 
    * A timeout SHOULD be handled by closing the underlying connection.
    * The Server SHOULD always attempt to store as much of the received data as possible.
    * 
    * The Server MUST respond with the 403 Forbidden status to PATCH requests against a final upload URL and 
    * MUST NOT modify the final or its partial uploads.
    * 
    * [Upload-Expires] This header MUST be included in every PATCH response if the upload is going to expire. 
    */

    internal class WriteFileHandler : IntentHandler
    {
        internal override Requirement[] Requires => GetListOfRequirements();

        private readonly ExpirationHelper _expirationHelper;
        private readonly ChecksumHelper _checksumHelper;
        private readonly bool _initiatedFromCreationWithUpload;

        public WriteFileHandler(ContextAdapter context, bool initiatedFromCreationWithUpload)
            : base(context, IntentType.WriteFile, LockType.RequiresLock)
        {
            _checksumHelper = new ChecksumHelper(Context);
            _expirationHelper = new ExpirationHelper(Context);
            _initiatedFromCreationWithUpload = initiatedFromCreationWithUpload;
        }

        internal override async Task Invoke()
        {
            await WriteUploadLengthIfDefered();

#if pipelines

            var writeTuple = await HandlePipelineWrite();

#else
            var writeTuple = await HandleStreamWrite();
#endif

            var bytesWritten = writeTuple.Item1;
            var cancellationToken = writeTuple.Item2;

            var expires = _expirationHelper.IsSlidingExpiration
                ? await _expirationHelper.SetExpirationIfSupported(Request.FileId, cancellationToken)
                : await _expirationHelper.GetExpirationIfSupported(Request.FileId, cancellationToken);

            var matchChecksumResult = await _checksumHelper.MatchChecksum(cancellationToken.IsCancellationRequested);

            if (matchChecksumResult.IsFailure())
            {
                await Response.Error(matchChecksumResult.Status, matchChecksumResult.ErrorMessage);
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var fileOffset = long.Parse(Request.GetHeader(HeaderConstants.UploadOffset));

            Response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            Response.SetHeader(HeaderConstants.UploadOffset, (fileOffset + bytesWritten).ToString());

            if (expires.HasValue)
            {
                Response.SetHeader(HeaderConstants.UploadExpires, ExpirationHelper.FormatHeader(expires));
            }

            Response.SetStatus(HttpStatusCode.NoContent);

            if (await FileIsComplete(Request.FileId, fileOffset, bytesWritten))
            {
                if (!await IsPartialUpload())
                {
                    await EventHelper.NotifyFileComplete(Context);
                }
            }
        }

#if pipelines

        private async Task<Tuple<long, CancellationToken>> HandlePipelineWrite()
        {
            long bytesWritten;
            CancellationToken cancellationToken;
            var isSupported = Context.Configuration.UsePipelinesIfAvailable && StoreAdapter.Features.Pipelines;
            if (isSupported)
            {
                var guardedPipeReader = new ClientDisconnectGuardedPipeReader(Request.BodyReader, CancellationToken);
                bytesWritten = await StoreAdapter.AppendDataAsync(Request.FileId, guardedPipeReader, CancellationToken);
                cancellationToken = CancellationToken;

                return new Tuple<long, CancellationToken>(bytesWritten, cancellationToken);
            }
            else
            {
                return await HandleStreamWrite();
            }
        }

#endif

        private async Task<Tuple<long, CancellationToken>> HandleStreamWrite()
        {
            var guardedStream = new ClientDisconnectGuardedReadOnlyStream(Request.Body, CancellationTokenSource.CreateLinkedTokenSource(CancellationToken));
            var bytesWritten = await Store.AppendDataAsync(Request.FileId, guardedStream, guardedStream.CancellationToken);
            var cancellationToken = guardedStream.CancellationToken;

            return new Tuple<long, CancellationToken>(bytesWritten, cancellationToken);
        }

        private Task WriteUploadLengthIfDefered()
        {
            var uploadLenghtHeader = Request.GetHeader(HeaderConstants.UploadLength);
            if (uploadLenghtHeader != null && StoreAdapter.Extensions.CreationDeferLength)
            {
                return StoreAdapter.SetUploadLengthAsync(Request.FileId, long.Parse(uploadLenghtHeader), Context.CancellationToken);
            }

            return TaskHelper.Completed;
        }

        private Task<bool> IsPartialUpload()
        {
            if (!StoreAdapter.Extensions.Concatenation)
            {
                return Task.FromResult(false);
            }

            return IsPartialUploadLocal();

            async Task<bool> IsPartialUploadLocal()
            {
                var concat = await StoreAdapter.GetUploadConcatAsync(Request.FileId, CancellationToken);

                return concat is FileConcatPartial;
            }
        }

        private async Task<bool> FileIsComplete(string fileId, long fileOffset, long bytesWritten)
        {
            var fileUploadLength = await Store.GetUploadLengthAsync(fileId, CancellationToken);
            return fileOffset + bytesWritten == fileUploadLength;
        }

        private Requirement[] GetListOfRequirements()
        {
            var contentTypeRequirement = new ContentType();
            var uploadLengthRequirement = new UploadLengthForWriteFile();
            var uploadChecksumRequirement = new UploadChecksum(_checksumHelper);
            var fileHasExpired = new FileHasNotExpired();

            // Initiated using creation-with-upload meaning that we can guarantee that the file already exist, the offset is correct etc.
            if (_initiatedFromCreationWithUpload)
            {
                return new Requirement[]
                {
                    contentTypeRequirement,
                    uploadLengthRequirement,
                    uploadChecksumRequirement,
                    fileHasExpired
                };
            }

            return new Requirement[]
            {
                new FileExist(),
                contentTypeRequirement,
                uploadLengthRequirement,
                new UploadOffset(),
                new UploadConcatForWriteFile(),
                uploadChecksumRequirement,
                fileHasExpired,
                new RequestOffsetMatchesFileOffset(),
                new FileIsNotCompleted()
            };
        }
    }
}
