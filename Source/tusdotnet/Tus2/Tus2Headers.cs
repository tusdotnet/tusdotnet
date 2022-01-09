#nullable enable


namespace tusdotnet.Tus2
{
    public class Tus2Headers
    {
        public long? UploadOffset { get; set; }

        public string? UploadToken { get; set; }

        public bool? UploadIncomplete { get; set; }
    }
}
