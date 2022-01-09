#nullable enable
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using tusdotnet.Models;

namespace tusdotnet.Tus2
{
    public interface IMetadataParser
    {
        Dictionary<string, Metadata>? Parse(HttpContext httpContext);
    }
}
