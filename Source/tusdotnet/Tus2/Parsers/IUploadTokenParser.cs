#nullable enable

namespace tusdotnet.Tus2
{
    internal interface IUploadTokenParser
    {
        string? Parse(string? uploadTokenHeader);
    }
}
