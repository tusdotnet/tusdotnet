using System;
using System.Collections.Generic;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Models.Concatenation;

namespace tusdotnet.Models.Configuration
{
    /// <summary>
    /// Context for the OnCreateComplete event
    /// </summary>
    public class CreateCompleteContext : EventContext<CreateCompleteContext>
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

        internal ContextAdapter Context { private get; set; }

        /// <summary>
        /// Calling this method will replace the default upload url that is provided to the client indicating where to upload the file data.
        /// Both relative and absolute URLs are supported.
        /// NOTE: tusdotnet is path based. The file id must the last item in the path (e.g. /files/myfiles/{fileId}).
        /// </summary>
        /// <param name="uploadUrl">The upload url to return to the client</param>
        public void SetUploadUrl(Uri uploadUrl)
        {
            Context.Response.SetHeader(HeaderConstants.Location, uploadUrl.ToString());
        }
    }
}
