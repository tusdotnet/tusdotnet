using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Extensions.Internal;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Models.Configuration;
using tusdotnet.Parsers;
using tusdotnet.Validation;

namespace tusdotnet.IntentHandlers
{
    /*
    * This extension can be used to concatenate multiple uploads into a single one enabling Clients to perform parallel uploads and 
    * to upload non-contiguous chunks. If the Server supports this extension, it MUST add concatenation to the Tus-Extension header.
    * A partial upload represents a chunk of a file. It is constructed by including the Upload-Concat: partial header 
    * while creating a new upload using the Creation extension. Multiple partial uploads are concatenated into a 
    * final upload in the specified order. The Server SHOULD NOT process these partial uploads until they are 
    * concatenated to form a final upload. The length of the final upload MUST be the sum of the length of all partial uploads.
    * In order to create a new final upload the Client MUST add the Upload-Concat header to the upload creation request. 
    * The value MUST be final followed by a semicolon and a space-separated list of the partial upload URLs that need to be concatenated. 
    * The partial uploads MUST be concatenated as per the order specified in the list. 
    * This concatenation request SHOULD happen after all of the corresponding partial uploads are completed.
    * The Client MUST NOT include the Upload-Length header in the final upload creation.
    * The Client MAY send the concatenation request while the partial uploads are still in progress.
    * This feature MUST be explicitly announced by the Server by adding concatenation-unfinished to the Tus-Extension header.
    * When creating a new final upload the partial uploads’ metadata SHALL NOT be transferred to the new final upload.
    * All metadata SHOULD be included in the concatenation request using the Upload-Metadata header.
    * The Server MAY delete partial uploads after concatenation. They MAY however be used multiple times to form a final resource. 
    * 
    * If the expiration is known at the creation, the Upload-Expires header MUST be included in the response to the initial POST request. 
     */
    internal class ConcatenateFilesHandler : IntentHandler
    {
        internal override Requirement[] Requires => BuildListOfRequirements();

        public UploadConcat UploadConcat { get; }

        private Dictionary<string, Metadata> _metadataFromRequirement;

        public ConcatenateFilesHandler(ContextAdapter context, ITusConcatenationStore concatenationStore, ITusClientTagStore clientTagStore, ITusChallengeStore challengeStore)
            : base(context, IntentType.ConcatenateFiles, LockType.NoLock)
        {
            UploadConcat = ParseUploadConcatHeader();
            _concatenationStore = concatenationStore;
            _expirationHelper = new ExpirationHelper(context.Configuration);
            _clientTagStore = clientTagStore;
            _challengeStore = challengeStore;
        }

        private readonly ITusConcatenationStore _concatenationStore;
        private readonly ExpirationHelper _expirationHelper;
        private readonly ITusClientTagStore _clientTagStore;
        private readonly ITusChallengeStore _challengeStore;

        internal override async Task<ResultType> Challenge(UploadChallengeParserResult uploadChallenge, ITusChallengeStoreHashFunction hashFunction, ITusChallengeStore challengeStore)
        {
            if (UploadConcat.Type is FileConcatPartial)
                return ResultType.ContinueExecution;

            var finalConcat = (FileConcatFinal)UploadConcat.Type;
            var partialChecksums = new List<string>(finalConcat.Files.Length);

            var partialSecrets = finalConcat.Files.Select(async partialFileId => await challengeStore.GetUploadSecretAsync(partialFileId, Context.CancellationToken));

            if (partialSecrets.Any(secret => secret != null) && uploadChallenge == null)
            {
                // TODO: 400 Bad request instead? Seems odd that this endpoint should not exist.
                Context.Response.NotFound();
                return ResultType.StopExecution;
            }

            foreach (var partialFileId in finalConcat.Files)
            {
                /*
                 * Upload-Challenge
	= "sha256" + " " + SHA256("sha256 sXfhFCwyWMjnH1DMPkArsByfa4FEGtpf3LsAt6uDkTU=" + "sha256 JbVm0kH59MDQfGtzjJ3s9oBjzHp+Yqtv7O2/OzYTUqg=")
	= "sha256 jWk0GUnLo2QNZaY3zHZ1N/Rgf7EWHtFI677w1mB5aMg="
                 * */

                var secret = await challengeStore.GetUploadSecretAsync(partialFileId, Context.CancellationToken);
                if (string.IsNullOrEmpty(secret))
                {
                    partialChecksums.Add(string.Empty);
                    continue;
                }

                // TODO Change 0 to #
                var partialHash = hashFunction?.CreateHash("POST0" + secret) ?? new byte[0];
                partialChecksums.Add(Convert.ToBase64String(partialHash));
            }

            var inputToHash = "";
            foreach (var item in partialChecksums)
            {
                inputToHash += $"{uploadChallenge.Algorithm} {item}";
            }

            if (!uploadChallenge.VerifyChecksum(inputToHash, hashFunction))
            {
                Context.Response.NotFound();
                return ResultType.StopExecution;
            }

            return ResultType.ContinueExecution;
        }

        internal override async Task Invoke()
        {
            var metadataString = Request.GetHeader(HeaderConstants.UploadMetadata);

            string fileId;
            DateTimeOffset? expires = null;

            var onBeforeCreateResult = await EventHelper.Validate<BeforeCreateContext>(Context, ctx =>
            {
                ctx.Metadata = _metadataFromRequirement;
                ctx.UploadLength = Request.UploadLength;
                ctx.FileConcatenation = UploadConcat.Type;
            });

            if (onBeforeCreateResult == ResultType.StopExecution)
            {
                return;
            }

            fileId = await HandleCreationOfConcatFiles(Request.UploadLength, metadataString, _metadataFromRequirement);

            if (IsPartialFile())
            {
                expires = await _expirationHelper.SetExpirationIfSupported(fileId, CancellationToken);
            }

            string uploadTag = null;
            if (_clientTagStore != null && (uploadTag = Request.GetHeader(HeaderConstants.UploadTag)) != null)
            {
                await _clientTagStore.SetClientTagAsync(fileId, uploadTag, Context.GetUsername());
            }

            string uploadSecret = null;
            if (_challengeStore != null && (uploadSecret = Request.GetHeader(HeaderConstants.UploadSecret)) != null)
            {
                await _challengeStore.SetUploadSecretAsync(fileId, uploadSecret, Context.CancellationToken);
            }

            Response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            Response.SetHeader(HeaderConstants.Location, Context.CreateLocationHeaderValue(fileId));

            if (expires != null)
            {
                Response.SetHeader(HeaderConstants.UploadExpires, _expirationHelper.FormatHeader(expires));
            }

            Response.SetStatus(HttpStatusCode.Created);
        }

        private Requirement[] BuildListOfRequirements()
        {
            var requirements = new List<Requirement>(3)
            {
                new Validation.Requirements.UploadConcatForConcatenateFiles(UploadConcat, _concatenationStore)
            };

            // Only validate upload length for partial files as the length of a final file is implicit.
            if (IsPartialFile())
            {
                requirements.Add(new Validation.Requirements.UploadLengthForCreateFileAndConcatenateFiles());
            }

            requirements.Add(new Validation.Requirements.UploadMetadata(metadata => _metadataFromRequirement = metadata));
            requirements.Add(new Validation.Requirements.ClientTagForPost(_clientTagStore));

            return requirements.ToArray();
        }

        private bool IsPartialFile()
        {
            return UploadConcat.Type is FileConcatPartial;
        }

        private UploadConcat ParseUploadConcatHeader()
        {
            return new UploadConcat(Request.GetHeader(HeaderConstants.UploadConcat), Context.Configuration.UrlPath);
        }

        private async Task<string> HandleCreationOfConcatFiles(long uploadLength, string metadataString, Dictionary<string, Metadata> metadata)
        {
            string createdFileId;

            if (UploadConcat.Type is FileConcatFinal finalConcat)
            {
                createdFileId = await _concatenationStore.CreateFinalFileAsync(finalConcat.Files, metadataString, CancellationToken);

                await EventHelper.Notify<CreateCompleteContext>(Context, ctx =>
                {
                    ctx.FileId = createdFileId;
                    ctx.Metadata = metadata;
                    ctx.UploadLength = uploadLength;
                    ctx.FileConcatenation = UploadConcat.Type;
                });

                await EventHelper.NotifyFileComplete(Context, ctx => ctx.FileId = createdFileId);
            }
            else
            {
                createdFileId = await _concatenationStore.CreatePartialFileAsync(uploadLength, metadataString, CancellationToken);

                await EventHelper.Notify<CreateCompleteContext>(Context, ctx =>
                {
                    ctx.FileId = createdFileId;
                    ctx.Metadata = metadata;
                    ctx.UploadLength = uploadLength;
                    ctx.FileConcatenation = UploadConcat.Type;
                });
            }

            return createdFileId;
        }
    }
}