using System;
using tusdotnet.Parsers;

namespace tusdotnet.Models
{
	/// <summary>
	/// Container for uploaded file checksum information.
	/// </summary>
	public class Checksum
	{
		/// <summary>
		/// The algorithm provided.
		/// </summary>
		public string Algorithm { get; set; }

		/// <summary>
		/// The checksum hash provided.
		/// </summary>
		public byte[] Hash { get; set; }

		/// <summary>
		/// True if the header value was parsable, otherwise false.
		/// </summary>
		public bool IsValid { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="Checksum"/> class.
		/// </summary>
		/// <param name="uploadChecksum">The Upload-Checksum header</param>
		public Checksum(string uploadChecksum)
		{
			var result = ChecksumParser.ParseAndValidate(uploadChecksum);
			IsValid = result.Success;
			Algorithm = result.Algorithm;
			Hash = result.Hash;
		}

		/// <summary>
		/// Used internally to setup a fallback when trailing checksum header is invalid.
		/// </summary>
		internal Checksum(string algorithm, byte[] hash)
        {
			Algorithm = algorithm;
			Hash = hash;
			IsValid = true;
        }
	}
}
