using System.Collections.Generic;
using tusdotnet.Models.Concatenation;

namespace tusdotnet.Models.Configuration
{
    /// <summary>
    /// Context for the OnBeforeCreate event
    /// </summary>
    public class BeforeCreateContext : ValidationContext<BeforeCreateContext>
    {
        /// <summary>
        /// The length (in bytes) of the file to be created. Will be -1 if Upload-Defer-Length is used.
        /// </summary>
        public long UploadLength { get; set; }

        /// <summary>
        /// True if Upload-Defer-Length is used in the request, otherwise false.
        /// </summary>
        public bool UploadLengthIsDeferred => UploadLength == -1;

        /// <summary>
        /// The metadata for the file.
        /// </summary>
        public Dictionary<string, Metadata> Metadata { get; set; }

        /// <summary>
        /// File concatenation information if the concatenation extension is used in the request,
        /// otherwise null.
        /// </summary>
        public FileConcat FileConcatenation { get; set; }
    }
}
