namespace tusdotnet.Models.Concatenation
{
	/// <summary>
	/// Represents the "partial" file concatenation type.
	/// </summary>
	public class FileConcatPartial : FileConcat
	{
		/// <summary>
		/// Returns the header value to send to the client for the specific file concatenation type.
		/// </summary>
		/// <returns>The header value to send to the client for the specific file concatenation type</returns>
		public override string GetHeader()
		{
			return "partial";
		}
	}
}
