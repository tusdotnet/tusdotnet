using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Models;

#if trailingheaders

using Microsoft.AspNetCore.Http;
using tusdotnet.Extensions.Internal;

#endif

namespace tusdotnet.Helpers
{
    internal class ChecksumHelper
    {
        private ChecksumHelperResult Ok { get; } = new(HttpStatusCode.OK, null);

        private readonly ContextAdapter _context;
        public Lazy<Task<List<string>>> _supportedAlgorithms;

#if trailingheaders

        private bool _checksumOriginatesFromTrailer;
        private ChecksumHelperResult _checksumTrailerParseResult;
        private Checksum _checksum;

#else

        private readonly Checksum _checksum;

#endif

        public ChecksumHelper(ContextAdapter context)
        {
            _context = context;

            if (!_context.StoreAdapter.Extensions.Checksum)
                return;

            _supportedAlgorithms = new Lazy<Task<List<string>>>(LoadSupportAlgorithms);

            var checksumHeader = _context.Request.Headers.UploadChecksum;

            if (checksumHeader != null)
            {
                _checksum = new Checksum(checksumHeader);
                if (_checksum.IsValid)
                {
                    context.Cache.UploadChecksum = _checksum;
                }
            }
        }

        private async Task<List<string>> LoadSupportAlgorithms() => (await _context.StoreAdapter.GetSupportedAlgorithmsAsync(_context.CancellationToken)).ToList();

        internal bool IsSupported() => _context.StoreAdapter.Extensions.Checksum;

#if trailingheaders

        internal ChecksumHelperResult VerifyStateForChecksumTrailer()
        {
            var hasDeclaredChecksumTrailer = _context.HasDeclaredTrailingUploadChecksumHeader();
            if (_checksum != null && hasDeclaredChecksumTrailer)
            {
                return new(HttpStatusCode.BadRequest, "Headers Upload-Checksum and trailing header Upload-Checksum are mutually exclusive and cannot be used in the same request");
            }

            if (hasDeclaredChecksumTrailer && !_context.HttpContext.Request.SupportsTrailers())
            {
                return new(HttpStatusCode.BadRequest, "Trailing header Upload-Checksum has been specified but http request does not support trailing headers");
            }

            return Ok;
        }

        private async Task SetChecksumFromTrailingHeader(bool clientDisconnected)
        {
            if (_checksum != null)
                return;

            if (!_context.StoreAdapter.Extensions.ChecksumTrailer)
                return;

            var checksumHeader = _context.GetTrailingUploadChecksumHeader();

            if (string.IsNullOrEmpty(checksumHeader))
            {
                // Fallback to force the store to discard the chunk.
                if (clientDisconnected && _context.HasDeclaredTrailingUploadChecksumHeader())
                {
                    _checksumOriginatesFromTrailer = true;
                    _checksumTrailerParseResult = Ok;
                    _checksum = ChecksumTrailerHelper.TrailingChecksumToUseIfRealTrailerIsFaulty;
                }

                return;
            }

            var tempChecksum = new Checksum(checksumHeader);

            _checksumOriginatesFromTrailer = true;
            _checksumTrailerParseResult = await VerifyHeader(tempChecksum);

            // Fallback to force the store to discard the chunk.
            if (_checksumTrailerParseResult.IsFailure())
            {
                tempChecksum = ChecksumTrailerHelper.TrailingChecksumToUseIfRealTrailerIsFaulty;
            }

            _checksumOriginatesFromTrailer = true;
            _checksum = tempChecksum;
        }

        internal bool SupportsChecksumTrailer() => true;
#else 

        internal ChecksumHelperResult VerifyStateForChecksumTrailer() => Ok;

        internal bool SupportsChecksumTrailer() => false;

#endif

        internal async Task<ChecksumHelperResult> MatchChecksum(bool clientDisconnected)
        {
            if (!_context.StoreAdapter.Extensions.Checksum)
            {
                return Ok;
            }

            var errorResponse = new ChecksumHelperResult((HttpStatusCode)460, "Header Upload-Checksum does not match the checksum of the file");

#if trailingheaders

            await SetChecksumFromTrailingHeader(clientDisconnected);

            if (_checksumOriginatesFromTrailer && _checksumTrailerParseResult.IsFailure())
            {
                errorResponse = _checksumTrailerParseResult;
            }
#endif

            if (_checksum == null)
            {
                return Ok;
            }

            var result = await _context.StoreAdapter.VerifyChecksumAsync(_context.FileId, _checksum.Algorithm, _checksum.Hash, _context.CancellationToken);

            if (!result)
            {
                return errorResponse;
            }

            return Ok;
        }

        internal Task<ChecksumHelperResult> VerifyLeadingHeader() => VerifyHeader(_checksum);

        private async Task<ChecksumHelperResult> VerifyHeader(Checksum providedChecksum)
        {
            if (providedChecksum == null)
            {
                return Ok;
            }

            if (!providedChecksum.IsValid)
            {
                return new(HttpStatusCode.BadRequest, $"Could not parse {HeaderConstants.UploadChecksum} header");
            }

            var checksumAlgorithms = await _supportedAlgorithms.Value;
            if (!checksumAlgorithms.Contains(providedChecksum.Algorithm))
            {
                return new(HttpStatusCode.BadRequest, $"Unsupported checksum algorithm. Supported algorithms are: {string.Join(",", checksumAlgorithms)}");
            }

            return Ok;
        }

        internal struct ChecksumHelperResult
        {
            internal HttpStatusCode Status { get; }

            internal string ErrorMessage { get; }

            internal ChecksumHelperResult(HttpStatusCode status, string errorMessage)
            {
                Status = status;
                ErrorMessage = errorMessage;
            }

            internal bool IsSuccess() => Status == HttpStatusCode.OK;

            internal bool IsFailure() => !IsSuccess();
        }
    }
}
