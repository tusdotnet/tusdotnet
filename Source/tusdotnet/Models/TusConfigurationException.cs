using System;

namespace tusdotnet.Models
{
	/// <summary>
	/// Exception thrown if an invalid configuration is used.
	/// </summary>
	public class TusConfigurationException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="TusConfigurationException"/> class.
		/// </summary>
		/// <param name="message">The message</param>
		public TusConfigurationException(string message) : base(message)
		{
			// Left blank.
		}
	}
}
