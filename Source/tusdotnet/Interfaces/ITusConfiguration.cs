using System;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
    public interface ITusConfiguration
    {
        /// <summary>
        /// The url path to listen for uploads on, e.g. "/files"
        /// If the site is located in a subpath (e.g. https://example.org/mysite) it must also be included (e.g. /mysite/files) 
        /// </summary>
        string UrlPath { get; }

        /// <summary>
        /// The store to use when storing files
        /// </summary>
        ITusStore Store { get; }

        /// <summary>
        /// Callback ran when a file is completely uploaded. 
        /// This callback is called only once after the last bytes have been written to the store.
        /// It will not be called for any subsequent upload requests for already completed files.
        /// </summary>
        Func<string, ITusStore, CancellationToken, Task> OnUploadCompleteAsync { get; }

        /// <summary>
        /// The maximum upload size to allow. Exceeding this limit will return a "413 Request Entity Too Large" error to the client.
        /// Set to null to allow any size. The size might still be restricted by the web server or operating system.
        /// </summary>
        int? MaxAllowedUploadSizeInBytes { get; }
    }
}