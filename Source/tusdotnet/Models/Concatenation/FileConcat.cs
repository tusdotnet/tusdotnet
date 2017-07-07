namespace tusdotnet.Models.Concatenation
{
	/// <summary>
	/// Base class for the different types of file concatenation.
	/// </summary>
	public abstract class FileConcat
	{
		/// <summary>
		/// Returns the header value to send to the client for the specific file concatenation type.
		/// </summary>
		/// <returns>The header value to send to the client for the specific file concatenation type</returns>
		public abstract string GetHeader();
	}
}
