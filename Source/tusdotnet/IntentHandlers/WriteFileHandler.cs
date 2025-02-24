using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Helpers;
using tusdotnet.Models;
using tusdotnet.Validation;
using tusdotnet.Validation.Requirements;
#if pipelines
using tusdotnet.Models.PipeReaders;
#endif

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

            var bytesWritten = await HandlePipelineWrite();

#else
            var bytesWritten = await HandleStreamWrite();
#endif

            var expires = _expirationHelper.IsSlidingExpiration
                ? await _expirationHelper.SetExpirationIfSupported(Context.FileId, Context.CancellationToken)
                : await _expirationHelper.GetExpirationIfSupported(Context.FileId, Context.CancellationToken);

            var matchChecksumResult = await _checksumHelper.MatchChecksum(Context.CancellationToken.IsCancellationRequested);

            if (matchChecksumResult.IsFailure())
            {
                Response.Error(matchChecksumResult.Status, matchChecksumResult.ErrorMessage);
                return;
            }

            if (Context.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            var initialOffset = Request.Headers.UploadOffset;
            var newUploadOffset = initialOffset + bytesWritten;

            Response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            Response.SetHeader(HeaderConstants.UploadOffset, newUploadOffset.ToString());

            Context.Cache.UploadOffset = newUploadOffset;

            if (expires.HasValue)
            {
                Response.SetHeader(HeaderConstants.UploadExpires, ExpirationHelper.FormatHeader(expires));
            }

            Response.SetResponse(HttpStatusCode.NoContent);
        }

#if pipelines

        private async Task<long> HandlePipelineWrite()
        {
            long bytesWritten;
            var isSupported = Context.Configuration.UsePipelinesIfAvailable && StoreAdapter.Features.Pipelines;
            if (isSupported)
            {
                var pipeReader = await GuardedPipeReaderFactory.Create(Context);
                bytesWritten = await StoreAdapter.AppendDataAsync(Context.FileId, pipeReader, CancellationToken);

                return bytesWritten;
            }
            else
            {
                return await HandleStreamWrite();
            }
        }

#endif

        private async Task<long> HandleStreamWrite()
        {
            var stream = await GuardedStreamFactory.Create(Context);

            var bytesWritten = await Store.AppendDataAsync(Context.FileId, stream, Context.CancellationToken);
            return bytesWritten;
        }

        private Task WriteUploadLengthIfDefered()
        {
            var uploadLengthHeader = Request.Headers[HeaderConstants.UploadLength];
            if (uploadLengthHeader != null && StoreAdapter.Extensions.CreationDeferLength)
            {
                return StoreAdapter.SetUploadLengthAsync(Context.FileId, long.Parse(uploadLengthHeader), Context.CancellationToken);
            }

            return TaskHelper.Completed;
        }

        private Requirement[] GetListOfRequirements()
        {
            var contentTypeRequirement = new ContentType();
            var uploadLengthRequirement = new UploadLengthForWriteFile();
            var uploadChecksumRequirement = new UploadChecksum(_checksumHelper);
            var fileHasNotExpired = new FileHasNotExpired();

            // Initiated using creation-with-upload meaning that we can guarantee that the file already exist, the offset is correct etc.
            if (_initiatedFromCreationWithUpload)
            {
                return [
                    contentTypeRequirement,
                    uploadLengthRequirement,
                    uploadChecksumRequirement,
                    fileHasNotExpired
                ];
            }

            return [
                new FileExist(),
                contentTypeRequirement,
                uploadLengthRequirement,
                new UploadOffset(),
                new UploadConcatForWriteFile(),
                uploadChecksumRequirement,
                fileHasNotExpired,
                new RequestOffsetMatchesFileOffset(),
                new FileIsNotCompleted()
            ];
        }
    }
}
