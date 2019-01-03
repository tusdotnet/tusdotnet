using System;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Models.Expiration;
using tusdotnet.Validation;
using tusdotnet.Validation.Requirements;

namespace tusdotnet.IntentHandlers
{
    internal class WriteFileHandler : IntentHandler
    {
        internal override Requirement[] Requires => new Requirement[]
        {
            new UploadConcatForWriteFile(),
            new ContentType(),
            new UploadLengthForWriteFile(),
            new UploadOffset(),
            new UploadChecksum(_checksum),
            new FileExist(),
            new FileHasNotExpired(),
            new RequestOffsetMatchesFileOffset(),
            new FileIsNotCompleted()
        };

        private readonly Checksum _checksum;

        private readonly ExpirationHelper _expirationHelper;

        public WriteFileHandler(ContextAdapter context)
            : base(context, IntentType.WriteFile, LockType.RequiresLock)
        {
            var checksumHeader = Request.GetHeader(HeaderConstants.UploadChecksum);

            if (checksumHeader != null)
            {
                _checksum = new Checksum(checksumHeader);
            }

            _expirationHelper = new ExpirationHelper(Context.Configuration);
        }

        internal override async Task Invoke()
        {
            await WriteUploadLengthIfDefered(Context);

            var bytesWritten = 0L;
            var clientDisconnected = await ClientDisconnectGuard.ExecuteAsync(
                async () =>
                {
                    bytesWritten = await Store.AppendDataAsync(Request.FileId, Request.Body, CancellationToken);
                },
                CancellationToken);

            if (clientDisconnected)
            {
                return;
            }

            var expires = _expirationHelper.IsSlidingExpiration
                ? await _expirationHelper.SetExpirationIfSupported(Request.FileId, CancellationToken)
                : await _expirationHelper.GetExpirationIfSupported(Request.FileId, CancellationToken);

            if (!await MatchChecksum())
            {
                await Response.Error((HttpStatusCode)460, "Header Upload-Checksum does not match the checksum of the file");
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

            if (await FileIsComplete(Request.FileId, fileOffset, bytesWritten)
                && !await IsPartialUpload(Context))
            {
                await EventHelper.NotifyFileComplete(Context);
            }
        }

        private Task WriteUploadLengthIfDefered(ContextAdapter Context)
        {
#warning TODO: Should this throw an exception?
            if (Context.Configuration.Store is ITusCreationDeferLengthStore creationDeferLengthStore && Context.Request.Headers.ContainsKey(HeaderConstants.UploadLength))
            {
                var uploadLength = long.Parse(Context.Request.GetHeader(HeaderConstants.UploadLength));
                return creationDeferLengthStore.SetUploadLengthAsync(Request.FileId, uploadLength, Context.CancellationToken);
            }

            return TaskHelper.Completed;
        }

        private static Task<bool> IsPartialUpload(ContextAdapter Context)
        {
            if (!(Context.Configuration.Store is ITusConcatenationStore concatenationStore))
            {
                return Task.FromResult(false);
            }

            return IsPartialUploadLocal();

            async Task<bool> IsPartialUploadLocal()
            {
                var concat = await concatenationStore.GetUploadConcatAsync(Context.GetFileId(), Context.CancellationToken);

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
    }
}
