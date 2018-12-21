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
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;
using tusdotnet.Validation;
using tusdotnet.Validation.Requirements;

namespace tusdotnet.IntentHandlers
{
    internal class WriteFileHandler : IntentHandler
    {
        internal override Requirement[] Requires => new Requirement[]
        {
            new Validation.Requirements.UploadConcat(),
            new ContentType(),
            new UploadLengthForWriteFile(),
            new UploadOffset(),
            new UploadChecksum(),
            new FileExist(),
            new FileHasNotExpired(),
            new RequestOffsetMatchesFileOffset(),
            new FileIsNotCompleted()
        };

        public WriteFileHandler(ContextAdapter context) 
            : base(context, IntentType.WriteFile, LockType.RequiresLock)
        {
        }

        internal override async Task<ResultType> Invoke()
        {
            var cancellationToken = Context.CancellationToken;
            var response = Context.Response;

            await WriteUploadLengthIfDefered(Context);

            var bytesWritten = 0L;
            var clientDisconnected = await ClientDisconnectGuard.ExecuteAsync(
                async () => bytesWritten = await Context.Configuration.Store.AppendDataAsync(Context.RequestFileId, Context.Request.Body, cancellationToken),
                cancellationToken);

            if (clientDisconnected)
            {
                return ResultType.Handled;
            }

            var expires = await GetOrUpdateExpires(Context);

            if (!await MatchChecksum(Context))
            {
                return await response.ErrorResult((HttpStatusCode)460, "Header Upload-Checksum does not match the checksum of the file");
            }

            var fileOffset = long.Parse(Context.Request.GetHeader(HeaderConstants.UploadOffset));

            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetHeader(HeaderConstants.UploadOffset, (fileOffset + bytesWritten).ToString());

            if (expires.HasValue)
            {
                response.SetHeader(HeaderConstants.UploadExpires, expires.Value.ToString("R"));
            }

            response.SetStatus((int)HttpStatusCode.NoContent);

            // Run OnUploadComplete if it has been provided.
            await RunOnUploadComplete(Context, fileOffset, bytesWritten);
            return ResultType.Handled;
        }

        private Task WriteUploadLengthIfDefered(ContextAdapter Context)
        {
#warning TODO: Should this throw an exception?
            if (Context.Configuration.Store is ITusCreationDeferLengthStore creationDeferLengthStore && Context.Request.Headers.ContainsKey(HeaderConstants.UploadLength))
            {
                var uploadLength = long.Parse(Context.Request.GetHeader(HeaderConstants.UploadLength));
                return creationDeferLengthStore.SetUploadLengthAsync(Context.RequestFileId, uploadLength, Context.CancellationToken);
            }
            #warning TODO Set common base result that can be returned
            return Task.FromResult(0);
        }

        private static Task RunOnUploadComplete(ContextAdapter Context, long fileOffset, long bytesWritten)
        {
            if (Context.Configuration.OnUploadCompleteAsync == null && Context.Configuration.Events?.OnFileCompleteAsync == null)
            {
                return Task.FromResult(0);
            }

            return RunOnUploadCompleteLocal();

            async Task RunOnUploadCompleteLocal()
            {
                if (await IsPartialUpload(Context))
                {
                    return;
                }

                var fileId = Context.GetFileId();
                var fileUploadLength = await Context.Configuration.Store.GetUploadLengthAsync(fileId, Context.CancellationToken);
                var fileIsComplete = fileOffset + bytesWritten == fileUploadLength;

                if (fileIsComplete)
                {
                    if (Context.Configuration.OnUploadCompleteAsync != null)
                    {
                        await Context.Configuration.OnUploadCompleteAsync(fileId, Context.Configuration.Store, Context.CancellationToken);
                    }

                    if (Context.Configuration.Events?.OnFileCompleteAsync != null)
                    {
                        await Context.Configuration.Events.OnFileCompleteAsync(FileCompleteContext.Create(Context));
                    }
                }
            }
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

        private static Task<bool> MatchChecksum(ContextAdapter Context)
        {
            if (!(Context.Configuration.Store is ITusChecksumStore checksumStore))
            {
                return Task.FromResult(true);
            }

            var checksumHeader = Context.Request.GetHeader(HeaderConstants.UploadChecksum);

            if (checksumHeader == null)
            {
                return Task.FromResult(true);
            }

            var providedChecksum = new Checksum(checksumHeader);

            return checksumStore.VerifyChecksumAsync(Context.GetFileId(), providedChecksum.Algorithm,
                providedChecksum.Hash, Context.CancellationToken);
        }

        private Task<DateTimeOffset?> GetOrUpdateExpires(ContextAdapter Context)
        {
            if (!(Context.Configuration.Store is ITusExpirationStore expirationStore))
            {
                return Task.FromResult((DateTimeOffset?)null);
            }

            if (!(Context.Configuration.Expiration is SlidingExpiration slidingExpiration))
            {
                return expirationStore.GetExpirationAsync(Context.RequestFileId, Context.CancellationToken);
            }

            return UpdateExpiredLocal();

            async Task<DateTimeOffset?> UpdateExpiredLocal()
            {
                var expires = DateTimeOffset.UtcNow.Add(slidingExpiration.Timeout);
                await expirationStore.SetExpirationAsync(Context.RequestFileId, expires, Context.CancellationToken);
                return expires;
            }
        }
    }
}
