namespace tusdotnet.Adapters
{
    internal interface ITrailingHeaderHelper
    {
        string? GetTrailingUploadChecksumHeader();
        bool HasDeclaredTrailingUploadChecksumHeader();
    }
}
