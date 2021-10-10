namespace tusdotnet.Parsers
{
    internal sealed class ChecksumParserResult
    {
        /// <summary>
        /// True if the parsing was successful, otherwise false.
        /// </summary>
        internal bool Success { get; }

        internal string Algorithm { get; }

        internal byte[] Hash { get; }

        private ChecksumParserResult(bool success, string algorithm = null, byte[] hash = null)
        {
            Success = success;
            Algorithm = algorithm;
            Hash = hash;
        }

        internal static ChecksumParserResult FromError()
        {
            return new ChecksumParserResult(false);
        }

        internal static ChecksumParserResult FromResult(string algorithm, byte[] hash)
        {
            return new ChecksumParserResult(true, algorithm, hash);
        }

    }
}
