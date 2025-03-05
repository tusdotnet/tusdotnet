using System;

namespace tusdotnet.Models.Expiration
{
    /// <summary>
    /// Base class for expiration configuration
    /// </summary>
    public abstract class ExpirationBase
    {
        /// <summary>
        /// The time that the incomplete file can live withouth being flagged as expired.
        /// </summary>
        public TimeSpan Timeout { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:tusdotnet.Models.Expiration.ExpirationBase" /> class.
        /// </summary>
        /// <param name="timeout">The time that the incomplete file can live withouth being flagged as expired</param>
        protected ExpirationBase(TimeSpan timeout)
        {
            Timeout = timeout;
        }
    }
}
