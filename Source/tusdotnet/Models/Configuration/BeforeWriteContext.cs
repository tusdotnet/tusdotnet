namespace tusdotnet.Models.Configuration;

/// <summary>
/// Context for the OnBeforeWrite event
/// </summary>
public class BeforeWriteContext : ValidationContext<BeforeWriteContext>
{
    /// <summary>
    /// The current upload offset of the file.
    /// </summary>
    public long UploadOffset { get; set; }
}
