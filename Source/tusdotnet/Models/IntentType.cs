namespace tusdotnet.Models
{
    /// <summary>
    /// The different intents a request can have.
    /// </summary>
    public enum IntentType
    {
        /// <summary>
        /// Intent is to create a new file.
        /// </summary>
        CreateFile,
        
        /// <summary>
        /// Intent is to create a partial file or to concatenate multiple partial files into a single final file.
        /// </summary>
        ConcatenateFiles,

        /// <summary>
        /// Intent is to write data to an existing file.
        /// </summary>
        WriteFile,

        /// <summary>
        /// Intent is to delete an existing file.
        /// </summary>
        DeleteFile,

        /// <summary>
        /// Intent is to get information on an existing file, e.g. read current offset.
        /// </summary>
        GetFileInfo,

        /// <summary>
        /// Intent is to get server options, e.g. what extensions are supported.
        /// </summary>
        GetOptions
    }
}
