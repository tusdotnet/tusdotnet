#if trailingheaders

using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using tusdotnet.Adapters;
using tusdotnet.Extensions.Internal;
using tusdotnet.Models;

namespace tusdotnet.Helpers
{
    internal class ChecksumHelperWithTrailers : ChecksumHelper
    {
        private bool _checksumOriginatesFromTrailer;
        private ChecksumHelperResult _checksumTrailerParseResult;

        public ChecksumHelperWithTrailers(ContextAdapter context)
            : base(context) { }

        internal override ChecksumHelperResult VerifyStateForChecksumTrailer()
        {
            if (HasBothChecksumHeaderAndTrailer())
            {
                return CreateMutuallyExclusiveError();
            }

            if (HasUnsupportedTrailerRequest())
            {
                return CreateUnsupportedTrailerError();
            }

            return Ok;
        }

        internal override bool SupportsChecksumTrailer() => true;

        internal override async Task<ChecksumHelperResult> MatchChecksum(bool clientDisconnected)
        {
            if (!_context.StoreAdapter.Extensions.Checksum)
            {
                return Ok;
            }

            await SetChecksumFromTrailerIfNotAlreadySet(clientDisconnected);

            if (_checksum == null)
            {
                return Ok;
            }

            var checksumMatches = await VerifyChecksumAgainstStore();
            if (!checksumMatches)
            {
                return HasFailedTrailerParsing()
                    ? _checksumTrailerParseResult
                    : CreateChecksumMismatchError();
            }

            return Ok;
        }

        private bool HasBothChecksumHeaderAndTrailer()
        {
            return _checksum != null && _context.HasDeclaredTrailingUploadChecksumHeader();
        }

        private bool HasUnsupportedTrailerRequest()
        {
            return _context.HasDeclaredTrailingUploadChecksumHeader()
                && !_context.HttpContext.Request.SupportsTrailers();
        }

        private static ChecksumHelperResult CreateMutuallyExclusiveError()
        {
            return new(
                HttpStatusCode.BadRequest,
                "Headers Upload-Checksum and trailing header Upload-Checksum are mutually exclusive and cannot be used in the same request"
            );
        }

        private static ChecksumHelperResult CreateUnsupportedTrailerError()
        {
            return new(
                HttpStatusCode.BadRequest,
                "Trailing header Upload-Checksum has been specified but http request does not support trailing headers"
            );
        }

        private async Task SetChecksumFromTrailerIfNotAlreadySet(bool clientDisconnected)
        {
            if (!ShouldProcessTrailingChecksum())
                return;

            var checksumHeader = _context.GetTrailingUploadChecksumHeader();

            if (string.IsNullOrEmpty(checksumHeader))
            {
                HandleMissingTrailingChecksum(clientDisconnected);
                return;
            }

            await ProcessTrailingChecksum(checksumHeader);
        }

        private bool ShouldProcessTrailingChecksum()
        {
            return _checksum == null && _context.StoreAdapter.Extensions.ChecksumTrailer;
        }

        private void HandleMissingTrailingChecksum(bool clientDisconnected)
        {
            // Fallback to force the store to discard the chunk.
            if (clientDisconnected && _context.HasDeclaredTrailingUploadChecksumHeader())
            {
                _checksumOriginatesFromTrailer = true;
                _checksumTrailerParseResult = Ok;
                _checksum = ChecksumTrailerHelper.TrailingChecksumToUseIfRealTrailerIsFaulty;
            }
        }

        private async Task ProcessTrailingChecksum(string checksumHeader)
        {
            var tempChecksum = new Checksum(checksumHeader);

            _checksumOriginatesFromTrailer = true;
            _checksumTrailerParseResult = await VerifyHeader(tempChecksum);

            // Fallback to force the store to discard the chunk.
            if (_checksumTrailerParseResult.IsFailure())
            {
                tempChecksum = ChecksumTrailerHelper.TrailingChecksumToUseIfRealTrailerIsFaulty;
            }

            _checksum = tempChecksum;
        }

        private bool HasFailedTrailerParsing()
        {
            return _checksumOriginatesFromTrailer && _checksumTrailerParseResult.IsFailure();
        }
    }
}

#endif
