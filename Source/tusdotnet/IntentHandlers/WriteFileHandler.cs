using System.Net;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
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

        private readonly Checksum _checksum;

        private readonly ExpirationHelper _expirationHelper;
        private readonly bool _initiatedFromCreationWithUpload;

        public WriteFileHandler(ContextAdapter context, bool initiatedFromCreationWithUpload)
            : base(context, IntentType.WriteFile, LockType.RequiresLock)
        {
            var checksumHeader = Request.GetHeader(HeaderConstants.UploadChecksum);

            if (checksumHeader != null)
            {
                _checksum = new Checksum(checksumHeader);
            }

            _expirationHelper = new ExpirationHelper(Context.Configuration);
            _initiatedFromCreationWithUpload = initiatedFromCreationWithUpload;
        }

        internal override async Task Invoke()
        {
            await WriteUploadLengthIfDefered();

            var guardedStream = new ClientDisconnectGuardedReadOnlyStream(Request.Body, CancellationTokenSource.CreateLinkedTokenSource(CancellationToken));
            var bytesWritten = await Store.AppendDataAsync(Request.FileId, guardedStream, guardedStream.CancellationToken);

            var expires = _expirationHelper.IsSlidingExpiration
                ? await _expirationHelper.SetExpirationIfSupported(Request.FileId, CancellationToken)
                : await _expirationHelper.GetExpirationIfSupported(Request.FileId, CancellationToken);

            if (!await MatchChecksum())
            {
                await Response.Error((HttpStatusCode)460, "Header Upload-Checksum does not match the checksum of the file");
                return;
            }

            if (guardedStream.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            var fileOffset = long.Parse(Request.GetHeader(HeaderConstants.UploadOffset));

            Response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            Response.SetHeader(HeaderConstants.UploadOffset, (fileOffset + bytesWritten).ToString());

            if (expires.HasValue)
            {
                Response.SetHeader(HeaderConstants.UploadExpires, _expirationHelper.FormatHeader(expires));
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

        private Task WriteUploadLengthIfDefered()
        {
            var uploadLenghtHeader = Request.GetHeader(HeaderConstants.UploadLength);
            if (uploadLenghtHeader != null && Store is ITusCreationDeferLengthStore creationDeferLengthStore)
            {
                return creationDeferLengthStore.SetUploadLengthAsync(Request.FileId, long.Parse(uploadLenghtHeader), Context.CancellationToken);
            }

            return TaskHelper.Completed;
        }

        private Task<bool> IsPartialUpload()
        {
            if (!(Store is ITusConcatenationStore concatenationStore))
            {
                return Task.FromResult(false);
            }

            return IsPartialUploadLocal();

            async Task<bool> IsPartialUploadLocal()
            {
                var concat = await concatenationStore.GetUploadConcatAsync(Request.FileId, CancellationToken);

                return concat is FileConcatPartial;
            }
        }

        private async Task<bool> FileIsComplete(string fileId, long fileOffset, long bytesWritten)
        {
            var fileUploadLength = await Store.GetUploadLengthAsync(fileId, CancellationToken);
            return fileOffset + bytesWritten == fileUploadLength;
        }

        private Task<bool> MatchChecksum()
        {
            if (!(Store is ITusChecksumStore checksumStore))
            {
                return Task.FromResult(true);
            }

            if (_checksum == null)
            {
                return Task.FromResult(true);
            }

            return checksumStore.VerifyChecksumAsync(
                Request.FileId,
                _checksum.Algorithm,
                _checksum.Hash,
                CancellationToken);
        }

        private Requirement[] GetListOfRequirements()
        {
            var contentTypeRequirement = new ContentType();
            var uploadLengthRequirement = new UploadLengthForWriteFile();
            var uploadChecksumRequirement = new UploadChecksum(_checksum);
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
