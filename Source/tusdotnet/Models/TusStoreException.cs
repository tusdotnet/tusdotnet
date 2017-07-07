using System;

namespace tusdotnet.Models
{
	/// <summary>
	/// Exception thrown by a store if the store wishes to send a message to the client.
	/// All TusStoreExceptions will result in a 400 Bad Request response with the exception message
	/// as the response body.
	/// </summary>
	public class TusStoreException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="TusStoreException"/> class.
		/// </summary>
		/// <param name="message">The message. This message will be returned to the client.</param>
		public TusStoreException(string message) : base(message)
		{
			// Left blank.
		}
	}
}
