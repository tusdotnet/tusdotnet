using Microsoft.AspNetCore.Http;

namespace tusdotnet.Tus2
{
    public record TusHandlerContext(
        ITus2Storage Store,
        IMetadataParser MetadataParser,
        bool AllowClientToDeleteFile,
        Tus2Headers Headers, 
        HttpContext HttpContext);
}
