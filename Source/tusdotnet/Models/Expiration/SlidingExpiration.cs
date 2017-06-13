using System;

namespace tusdotnet.Models.Expiration
{
    public class SlidingExpiration : ExpirationBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:tusdotnet.Models.Expiration.SlidingExpiration" /> class.
        /// </summary>
        /// <param name="timeout">The time that the incomplete file can live withouth being flagged as expired</param>
        public SlidingExpiration(TimeSpan timeout) : base(timeout)
        {
            // Left blank
        }
    }
}
