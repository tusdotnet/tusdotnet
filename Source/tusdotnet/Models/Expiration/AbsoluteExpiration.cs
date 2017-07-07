using System;

namespace tusdotnet.Models.Expiration
{
	/// <summary>
	/// Absolute expiration that is only set once during file creation and is never updated after that.
	/// </summary>
	public class AbsoluteExpiration : ExpirationBase
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:tusdotnet.Models.Expiration.AbsoluteExpiration" /> class.
		/// </summary>
		/// <param name="timeout">The time that the incomplete file can live withouth being flagged as expired</param>
		public AbsoluteExpiration(TimeSpan timeout) : base(timeout)
		{
			// Left blank
		}
	}
}
