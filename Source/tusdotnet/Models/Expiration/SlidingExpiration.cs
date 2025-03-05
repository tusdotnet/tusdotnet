using System;

namespace tusdotnet.Models.Expiration
{
    /// <summary>
    /// Sliding expiration that set during creation and updated on every PATCH request.
    /// </summary>
    public class SlidingExpiration : ExpirationBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:tusdotnet.Models.Expiration.SlidingExpiration" /> class.
        /// </summary>
        /// <param name="timeout">The time that the incomplete file can live withouth being flagged as expired</param>
        public SlidingExpiration(TimeSpan timeout)
            : base(timeout)
        {
            // Left blank
        }
    }
}
