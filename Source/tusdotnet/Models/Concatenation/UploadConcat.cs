using tusdotnet.Parsers;

namespace tusdotnet.Models.Concatenation
{
    /// <summary>
    /// Container for uploaded file concatenation information.
    /// </summary>
    public class UploadConcat
	{
		/// <summary>
		/// The type of concatenation used. Is null if no concatenation info was provided or if the info is invalid.
		/// </summary>
		public FileConcat Type { get; }

		/// <summary>
		/// True if the header value was parsable and the info therein was valid, otherwise false.
		/// </summary>
		public bool IsValid { get; private set; }

		/// <summary>
		/// Parser error message. Null if <code>IsValid</code> is true.
		/// </summary>
		public string ErrorMessage { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="UploadConcat"/> class.
		/// This overload does not remove relative urls from the file ids when parsing.
		/// This overload should only be used inside a data store to save information regarding the concatenation type.
		/// </summary>
		/// <param name="uploadConcat">The Upload-Concat header</param>
		public UploadConcat(string uploadConcat) : this(uploadConcat, string.Empty)
		{
			// Left blank.
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="UploadConcat"/> class.
		/// This overload removes relative urls from the file ids when parsing and is used by tusdotnet when parsing
		/// the incoming Upload-Concat header.
		/// </summary>
		/// <param name="uploadConcat">The Upload-Concat header</param>
		/// <param name="urlPath">The UrlPath property in the ITusConfiguration</param>
		public UploadConcat(string uploadConcat, string urlPath)
		{
			IsValid = true;

			if (string.IsNullOrWhiteSpace(uploadConcat))
			{
				Type = null;
				return;
			}

			var result = UploadConcatParser.ParseAndValidate(uploadConcat, urlPath);

			IsValid = result.Success;
			ErrorMessage= result.ErrorMessage;
			Type = result.Type;
		}
	}
}
