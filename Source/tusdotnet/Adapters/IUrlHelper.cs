namespace tusdotnet.Adapters
{
    internal interface IUrlHelper
    {
        bool UrlMatchesUrlPath(ContextAdapter context);

        bool UrlMatchesFileIdUrl(ContextAdapter context);

        string ParseFileId(ContextAdapter context);
    }
}
