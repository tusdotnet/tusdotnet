using System.IO;
#if netstandard
using System.Reflection;
#endif

namespace tusdotnet.Extensions
{
	// ReSharper disable once InconsistentNaming - Consistent with exception name
	internal static class IOExceptionExtensions
	{

#if netstandard

	public static bool ClientDisconnected(this IOException exception)
	{
		if (exception.InnerException == null)
		{
			return false;
		}

		var innerType = exception.InnerException.GetType();

		if (!innerType.FullName.Equals("Microsoft.AspNetCore.Server.Kestrel.Internal.Networking.UvException"))
		{
			return false;
		}

		var status = (int) innerType.GetProperty("StatusCode").GetValue(exception.InnerException);

		return status == -4077;
	}

#endif

#if netfull

		public static bool ClientDisconnected(this IOException exception)
		{
			return exception.InnerException is System.Net.HttpListenerException;
		}

#endif

	}
}
