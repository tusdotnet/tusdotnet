using System;

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
			var temp = uploadChecksum.Split(' ');

			if (temp.Length != 2)
			{
				IsValid = false;
				return;
			}

			if (string.IsNullOrWhiteSpace(temp[0]))
			{
				IsValid = false;
				return;
			}

			var algorithm = temp[0].Trim();

			if (string.IsNullOrWhiteSpace(temp[1]))
			{
				IsValid = false;
				return;
			}

			try
			{
				Hash = Convert.FromBase64String(temp[1]);
				Algorithm = algorithm;
				IsValid = true;
			}
			catch
			{
				IsValid = false;
			}
		}
	}
}
