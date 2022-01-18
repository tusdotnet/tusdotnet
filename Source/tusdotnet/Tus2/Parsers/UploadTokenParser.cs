#nullable enable

namespace tusdotnet.Tus2
{
    internal class UploadTokenParser : IUploadTokenParser
    {
        public string? Parse(string? uploadTokenHeader)
        {
            return uploadTokenHeader.FromSfBinary();
        }
    }
}
