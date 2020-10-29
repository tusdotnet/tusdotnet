namespace tusdotnet.Constants
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static class HeaderConstants
    {
        public const string TusResumable = "Tus-Resumable";
        public const string TusResumableValue = "1.0.0";
        public const string TusVersion = "Tus-Version";
        public const string TusExtension = "Tus-Extension";
        public const string TusMaxSize = "Tus-Max-Size";
        public const string TusChecksumAlgorithm = "Tus-Checksum-Algorithm";
        public const string TusChallengeAlgorithm = "Tus-Challenge-Algorithm";

        public const string XHttpMethodOveride = "X-HTTP-Method-Override";

        public const string UploadLength = "Upload-Length";

        public const string UploadDeferLength = "Upload-Defer-Length";
        public const string UploadOffset = "Upload-Offset";
        public const string UploadMetadata = "Upload-Metadata";
        public const string UploadChecksum = "Upload-Checksum";
        public const string UploadConcat = "Upload-Concat";
        public const string UploadExpires = "Upload-Expires";
        public const string UploadTag = "Upload-Tag";
        public const string UploadChallenge = "Upload-Challenge";
        public const string UploadSecret = "Upload-Secret";

        // TODO: Replace with hashset
        internal static readonly char[] ValidUploadSecretChars = "!\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~".ToCharArray();

        public const string CacheControl = "Cache-Control";
        public const string NoStore = "no-store";
        public const string Location = "location";

        public const string ContentType = "Content-Type";
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
