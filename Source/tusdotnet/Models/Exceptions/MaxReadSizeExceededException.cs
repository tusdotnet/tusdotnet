namespace tusdotnet.Models
{
    internal class MaxReadSizeExceededException : TusStoreException
    {
        internal enum SizeSourceType
        {
            UploadLength,
            TusMaxSize
        }

        internal MaxReadSizeExceededException(SizeSourceType sizeSource)
            : base(GetMessage(sizeSource)) { }

        private static string GetMessage(SizeSourceType sizeSource)
        {
            return sizeSource == SizeSourceType.UploadLength
                ? "Request contains more data than the file's upload length"
                : "Request exceeds the server's max file size";
        }
    }
}
