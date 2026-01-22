using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Models;

namespace tusdotnet.Helpers
{
    internal class ChecksumHelper
    {
        protected static readonly ChecksumHelperResult Ok = new(HttpStatusCode.OK, null);

        protected readonly ContextAdapter _context;
        private Lazy<Task<List<string>>> _supportedAlgorithms;
        protected Checksum _checksum;

        public ChecksumHelper(ContextAdapter context)
        {
            _context = context;

            if (!_context.StoreAdapter.Extensions.Checksum)
                return;

            _supportedAlgorithms = new Lazy<Task<List<string>>>(LoadSupportAlgorithms);
            _checksum = TryParseChecksumHeader(context);
        }

        private Checksum TryParseChecksumHeader(ContextAdapter context)
        {
            var checksumHeader = context.Request.Headers.UploadChecksum;
            if (checksumHeader == null)
                return null;

            var checksum = new Checksum(checksumHeader);
            if (checksum.IsValid)
            {
                context.ParsedRequest.UploadChecksum = checksum;
            }

            return checksum;
        }

        private async Task<List<string>> LoadSupportAlgorithms() =>
            (
                await _context.StoreAdapter.GetSupportedAlgorithmsAsync(_context.CancellationToken)
            ).ToList();

        internal bool IsSupported() => _context.StoreAdapter.Extensions.Checksum;

        internal virtual ChecksumHelperResult VerifyStateForChecksumTrailer() => Ok;

        internal virtual bool SupportsChecksumTrailer() => false;

        internal virtual async Task<ChecksumHelperResult> MatchChecksum(bool clientDisconnected)
        {
            if (!_context.StoreAdapter.Extensions.Checksum)
            {
                return Ok;
            }

            if (_checksum == null)
            {
                return Ok;
            }

            var checksumMatches = await VerifyChecksumAgainstStore();
            if (!checksumMatches)
            {
                return CreateChecksumMismatchError();
            }

            return Ok;
        }

        protected async Task<bool> VerifyChecksumAgainstStore()
        {
            return await _context.StoreAdapter.VerifyChecksumAsync(
                _context.FileId,
                _checksum.Algorithm,
                _checksum.Hash,
                _context.CancellationToken
            );
        }

        protected static ChecksumHelperResult CreateChecksumMismatchError()
        {
            return new ChecksumHelperResult(
                (HttpStatusCode)460,
                "Header Upload-Checksum does not match the checksum of the file"
            );
        }

        internal Task<ChecksumHelperResult> VerifyLeadingHeader() => VerifyHeader(_checksum);

        protected async Task<ChecksumHelperResult> VerifyHeader(Checksum providedChecksum)
        {
            if (providedChecksum == null)
            {
                return Ok;
            }

            if (!providedChecksum.IsValid)
            {
                return CreateInvalidChecksumFormatError();
            }

            if (!await IsAlgorithmSupported(providedChecksum.Algorithm))
            {
                return await CreateUnsupportedAlgorithmError(providedChecksum.Algorithm);
            }

            return Ok;
        }

        private static ChecksumHelperResult CreateInvalidChecksumFormatError()
        {
            return new(
                HttpStatusCode.BadRequest,
                $"Could not parse {HeaderConstants.UploadChecksum} header"
            );
        }

        private async Task<bool> IsAlgorithmSupported(string algorithm)
        {
            var checksumAlgorithms = await _supportedAlgorithms.Value;
            return checksumAlgorithms.Contains(algorithm);
        }

        private async Task<ChecksumHelperResult> CreateUnsupportedAlgorithmError(string algorithm)
        {
            var checksumAlgorithms = await _supportedAlgorithms.Value;
            return new(
                HttpStatusCode.BadRequest,
                $"Unsupported checksum algorithm. Supported algorithms are: {string.Join(",", checksumAlgorithms)}"
            );
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
