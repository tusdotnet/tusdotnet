using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Extensions.Internal;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Parsers;

namespace tusdotnet.Authenticators
{
    internal class ChallengeAuthenticator
    {
        public async Task<ResultType> Authenticate(ContextAdapter context, IntentType intent)
        {
            if (!(context.Configuration.Store is ITusChallengeStore challengeStore))
                return ResultType.ContinueExecution;

            var intentSupportsNonFileIdRequests = intent == IntentType.GetFileInfo || intent == IntentType.ConcatenateFiles;

            var challenge = context.Request.GetHeader(HeaderConstants.UploadChallenge);

            if (!intentSupportsNonFileIdRequests)
            {
                if (await challengeStore.GetUploadSecretAsync(context.Request.FileId, context.CancellationToken) == null)
                    return ResultType.ContinueExecution;

                if (string.IsNullOrWhiteSpace(challenge))
                {
                    context.Response.NotFound();
                    return ResultType.StopExecution;
                }
            }

            var supportedAlgorithms = await challengeStore.GetSupportedAlgorithmsAsync(context.CancellationToken);
            var parsedUploadChallenge = UploadChallengeParser.ParseAndValidate(challenge, supportedAlgorithms);
            if (!parsedUploadChallenge.Success)
            {
                await context.Response.Error(HttpStatusCode.BadRequest, parsedUploadChallenge.ErrorMessage);
                return ResultType.StopExecution;
            }

            if (intent == IntentType.GetFileInfo)
                return await RunAuthenticateForGetFileInfo(context, parsedUploadChallenge, challengeStore);

            if (intent == IntentType.ConcatenateFiles)
                return await RunAuthenticationForConcatenateFiles(context, parsedUploadChallenge, challengeStore);

            if (string.IsNullOrEmpty(context.Request.FileId))
                return ResultType.ContinueExecution;

            return await RunAuthentication(context, parsedUploadChallenge, challengeStore);
        }

        private async Task<ResultType> RunAuthenticationForConcatenateFiles(ContextAdapter context, UploadChallengeParserResult parsedUploadChallenge, ITusChallengeStore challengeStore)
        {
            var uploadConcatHeader = context.Request.GetHeader(HeaderConstants.UploadConcat);

            if (string.IsNullOrEmpty(uploadConcatHeader))
                return ResultType.ContinueExecution;

            var fileConcat = new UploadConcat(uploadConcatHeader, context.Configuration.UrlPath);

            // TODO: What to do here?
            if (!fileConcat.IsValid)
            {
                return ResultType.ContinueExecution;
            }

            if (fileConcat.Type is FileConcatPartial)
                return ResultType.ContinueExecution;

            var concatenationStore = (ITusConcatenationStore)context.Configuration.Store;
            var finalConcat = (FileConcatFinal)fileConcat.Type;

            var partialChecksums = new List<string>(finalConcat.Files.Length);
            foreach (var partialFile in finalConcat.Files)
            {
                /*
                 * Upload-Challenge
	= "sha256" + " " + SHA256("sha256 sXfhFCwyWMjnH1DMPkArsByfa4FEGtpf3LsAt6uDkTU=" + "sha256 JbVm0kH59MDQfGtzjJ3s9oBjzHp+Yqtv7O2/OzYTUqg=")
	= "sha256 jWk0GUnLo2QNZaY3zHZ1N/Rgf7EWHtFI677w1mB5aMg="
                 * */

                if (await challengeStore.GetUploadSecretAsync(partialFile, context.CancellationToken) == null)
                {
                    partialChecksums.Add(string.Empty);
                    continue;
                }

                // TODO Change 0 to null
                var checksum = await CalculateChecksum("0", "POST", partialFile, parsedUploadChallenge.Algorithm, challengeStore, context.CancellationToken);
                partialChecksums.Add(Convert.ToBase64String(checksum));
            }

            var hasher = await challengeStore.GetHashFunctionAsync(parsedUploadChallenge.Algorithm, context.CancellationToken);
            var inputToHash = "";
            foreach (var item in partialChecksums)
            {
                inputToHash += $"{parsedUploadChallenge.Algorithm} {item}";
            }
            var finalChecksum = hasher.CreateHash(inputToHash);

            if (!ChallengeIsCorrect(parsedUploadChallenge, finalChecksum))
            {
                context.Response.NotFound();
                return ResultType.StopExecution;
            }

            return ResultType.ContinueExecution;
        }

        private async Task<ResultType> RunAuthentication(ContextAdapter context, UploadChallengeParserResult parsedUploadChallenge, ITusChallengeStore challengeStore)
        {
            var calculatedChecksum = await CalculateChecksum(context, challengeStore, parsedUploadChallenge);

            if (!ChallengeIsCorrect(parsedUploadChallenge, calculatedChecksum))
            {
                context.Response.NotFound();
                return ResultType.StopExecution;
            }

            // TODO: Good enough of handle this in some other way? Generic request cache?
            context.Request.UploadChallengeProvidedAndPassed = true;

            return ResultType.ContinueExecution;
        }

        private bool ChallengeIsCorrect(UploadChallengeParserResult parsedUploadChallenge, byte[] calculatedChecksum)
        {
            return parsedUploadChallenge.Hash.SequenceEqual(calculatedChecksum);
        }

        private async Task<ResultType> RunAuthenticateForGetFileInfo(ContextAdapter context, UploadChallengeParserResult parsedUploadChallenge, ITusChallengeStore challengeStore)
        {
            var uploadTag = context.Request.GetHeader(HeaderConstants.UploadTag);

            string originalFileId = context.Request.FileId;
            if (string.IsNullOrEmpty(originalFileId))
            {
                originalFileId = null;
            }

            string fileId = null;

            if (context.Configuration.Store is ITusClientTagStore clientTagStore)
            {
                if (string.IsNullOrEmpty(uploadTag) && string.IsNullOrEmpty(context.Request.FileId))
                {
                    context.Response.NotFound();
                    return ResultType.StopExecution;
                }

                if (string.IsNullOrEmpty(context.Request.FileId))
                {
                    var fileIdMap = await clientTagStore.ResolveUploadTagToFileIdAsync(uploadTag);
                    if (fileIdMap == null)
                    {
                        context.Response.NotFound();
                        return ResultType.StopExecution;
                    }

                    fileId = fileIdMap.FileId;
                }
            }

            if (fileId != null)
                context.Request.SetFileId(fileId);

            var result = await RunAuthentication(context, parsedUploadChallenge, challengeStore);

            if (fileId != null)
                context.Request.SetFileId(originalFileId);

            return result;
        }

        private Task<byte[]> CalculateChecksum(ContextAdapter context, ITusChallengeStore challengeStore, UploadChallengeParserResult challenge)
        {
            var uploadOffset = context.Request.GetHeader(HeaderConstants.UploadOffset) ?? "#";
            var httpMethod = context.Request.GetHttpMethod().ToUpper();

            return CalculateChecksum(uploadOffset, httpMethod, context.Request.FileId, challenge.Algorithm, challengeStore, context.CancellationToken);
        }

        private async Task<byte[]> CalculateChecksum(string uploadOffsetHeader, string httpMethod, string fileId, string challengeAlgorithm, ITusChallengeStore challengeStore, CancellationToken cancellationToken)
        {
            uploadOffsetHeader ??= "#";
            httpMethod = httpMethod.ToUpper();

            var secret = await challengeStore.GetUploadSecretAsync(fileId, cancellationToken);
            var hasher = await challengeStore.GetHashFunctionAsync(challengeAlgorithm, cancellationToken);

            return hasher.CreateHash(httpMethod + uploadOffsetHeader + secret);
        }
    }
}
