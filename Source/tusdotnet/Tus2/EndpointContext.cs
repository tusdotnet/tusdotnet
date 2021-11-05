using Microsoft.AspNetCore.Http;

namespace tusdotnet.Tus2
{
    internal record EndpointContext(
        Tus2DiskStore Store, 
        Tus2Headers Headers, 
        HttpContext HttpContext, 
        OngoingUploadTransferServiceDiskBased OngoingUploadTransfer);
}
