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

        /// <summary>
        /// Set an expiration time where incomplete files can no longer be updated.
        /// This value can either be <code>AbsoluteExpiration</code> or <code>SlidingExpiration</code>.
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
        /// Check that the config is valid. Throws a <exception cref="TusConfigurationException">TusConfigurationException</exception> if the config is invalid.
        /// </summary>
        internal void Validate()
        {
            if (Store == null)
            {
                throw new TusConfigurationException($"{nameof(Store)} cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(UrlPath))
            {
                throw new TusConfigurationException($"{nameof(UrlPath)} cannot be empty.");
            }

            if (!Enum.IsDefined(typeof(MetadataParsingStrategy), MetadataParsingStrategy))
            {
                throw new TusConfigurationException($"{nameof(MetadataParsingStrategy)} is not a valid value.");
            }
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