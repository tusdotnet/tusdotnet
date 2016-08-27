using System;

namespace tusdotnet.Models
{
	public class TusStoreException : ApplicationException
	{
		public TusStoreException(string message) : base(message)
		{
			// Left blank.
		}
	}
}
