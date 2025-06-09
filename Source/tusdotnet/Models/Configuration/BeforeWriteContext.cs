namespace tusdotnet.Models.Configuration;

/// <summary>
/// Context for the OnBeforeWrite event
/// </summary>
public class BeforeWriteContext : ValidationContext<BeforeWriteContext>
{
    /// <summary>
    /// The length (in bytes) of the file being created.
    /// </summary>
    public long UploadLength { get; set; }
    
    /// <summary>
    /// The length (in bytes) of the currently uploaded file size.
    /// </summary>
    public long UploadOffset { get; set; }
}