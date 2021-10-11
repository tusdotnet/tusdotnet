using tusdotnet.Models.Concatenation;

namespace tusdotnet.Parsers
{
    internal class UploadConcatParserResult
    {
        /// <summary>
        /// True if the parsing was successful, otherwise false.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Error mesage if <see cref="Success"/> is false, otherwise null.
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
		/// The type of concatenation used. Is null if no concatenation info was provided or if the info is invalid.
		/// </summary>
		public FileConcat Type { get; }

        private UploadConcatParserResult(bool success, string errorMessage, FileConcat type)
        {
            Success = success;
            ErrorMessage = errorMessage;
            Type = type;
        }

        internal static UploadConcatParserResult FromError(string errorMessage)
        {
            return new UploadConcatParserResult(false, errorMessage, null);
        }

        internal static UploadConcatParserResult FromResult(FileConcat type)
        {
            return new UploadConcatParserResult(true, null, type);
        }
    }
}
