using System;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;

namespace tusdotnet.Models
{
    /// <summary>
    /// The default tusdotnet configuration.
    /// </summary>
    public class DefaultTusConfiguration
    {
        /// <summary>
        /// The url path to listen for uploads on (e.g. "/files").
        /// If the site is located in a subpath (e.g. https://example.org/mysite) it must also be included (e.g. /mysite/files)
        /// </summary>
        public virtual string UrlPath { get; set; }

        /// <summary>
        /// The store to use when storing files.
        /// </summary>
        public virtual ITusStore Store { get; set; }

        /// <summary>
        /// Callback ran when a file is completely uploaded.
        /// This callback is called only once after the last bytes have been written to the store.
        /// It will not be called for any subsequent upload requests for already completed files.
        /// </summary>
        [Obsolete("This callback is obsolete. Use DefaultTusConfiguration.Events.OnFileCompleteAsync instead.")]
        public virtual Func<string, ITusStore, CancellationToken, Task> OnUploadCompleteAsync { get; set; }

        /// <summary>
        /// Lock provider to use when locking to prevent files from being accessed while the file is still in use.
        /// Defaults to using in-memory locks.
        /// </summary>
        public virtual ITusFileLockProvider FileLockProvider { get; set; }

        /// <summary>
        /// Callbacks to run during different stages of the tusdotnet pipeline.
        /// </summary>
        public virtual Events Events { get; set; }

        /// <summary>
        /// The maximum upload size to allow. Exceeding this limit will return a "413 Request Entity Too Large" error to the client.
        /// Set to null to allow any size. The size might still be restricted by the web server or operating system.
        /// This property will be preceded by <see cref="MaxAllowedUploadSizeInBytesLong" />.
        /// </summary>
        public virtual int? MaxAllowedUploadSizeInBytes { get; set; }

        /// <summary>
        /// The maximum upload size to allow. Exceeding this limit will return a "413 Request Entity Too Large" error to the client.
        /// Set to null to allow any size. The size might still be restricted by the web server or operating system.
        /// This property will take precedence over <see cref="MaxAllowedUploadSizeInBytes" />.
        /// </summary>
        public virtual long? MaxAllowedUploadSizeInBytesLong { get; set; }

#if pipelines

        /// <summary>
        /// Use the incoming request's PipeReader instead of the stream to read data from the client.
        /// This is only available on .NET Core 3.1 or later and if the store supports it through the ITusPipelineStore interface.
        /// </summary>
        public virtual bool UsePipelinesIfAvailable { get; set; }

#endif

        /// <summary>
        /// Set an expiration time where incomplete files can no longer be updated.
        /// This value can either be <c>AbsoluteExpiration</c> or <c>SlidingExpiration</c>.
        /// Absolute expiration will be saved per file when the file is created.
        /// Sliding expiration will be saved per file when the file is created and updated on each time the file is updated.
        /// Setting this property to null will disable file expiration.
        /// </summary>
        public virtual ExpirationBase Expiration { get; set; }

        /// <summary>
        /// Set the strategy to use when parsing metadata. Defaults to <see cref="MetadataParsingStrategy.AllowEmptyValues"/> for better compatibility with tus clients.
        /// Change to <see cref="MetadataParsingStrategy.Original"/> to use the old format.
        /// </summary>
        public virtual MetadataParsingStrategy MetadataParsingStrategy { get; set; }

        /// <summary>
        /// Tus extensions allowed to use by the client. Defaults to <see cref="TusExtensions.All" />.
        /// In addition to being in this list the extension must also be supported by the store provided in <see cref="DefaultTusConfiguration.Store"/> to be accessible for the client.
        /// </summary>
        public TusExtensions AllowedExtensions { get; set; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public DefaultTusConfiguration()
        {
#if pipelines && NET6_0_OR_GREATER

            UsePipelinesIfAvailable = true;

#endif

            AllowedExtensions = TusExtensions.All;
        }

        /// <summary>
        /// Returns the maximum upload size to allow from <see cref="MaxAllowedUploadSizeInBytesLong" /> or <see cref="MaxAllowedUploadSizeInBytes"/> depending on which one is set.
        /// </summary>
        internal long? GetMaxAllowedUploadSizeInBytes()
        {
            return MaxAllowedUploadSizeInBytesLong ?? MaxAllowedUploadSizeInBytes;
        }

        private DateTimeOffset? _systemTime;

        internal void MockSystemTime(DateTimeOffset systemTime)
        {
            _systemTime = systemTime;
        }

        internal DateTimeOffset GetSystemTime()
        {
            return _systemTime ?? DateTimeOffset.UtcNow;
        }
    }
}