#nullable enable


namespace tusdotnet.Tus2
{
    public class Tus2Headers
    {
        public long? UploadOffset { get; set; }

        public string? ResourceId { get; set; }

        public bool? UploadComplete { get; set; }
    }
}
