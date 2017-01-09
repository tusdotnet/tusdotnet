using System;

namespace tusdotnet.Models
{
	internal class Checksum
	{
		public string Algorithm { get; set; }
		public byte[] Hash { get; set; }
		public bool IsValid { get; set; }
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

			Algorithm = temp[0].Trim();

			if (string.IsNullOrWhiteSpace(temp[1]))
			{
				IsValid = false;
				return;
			}

			try
			{
				Hash = Convert.FromBase64String(temp[1]);
				IsValid = true;
			}
			catch
			{
				IsValid = false;
			}
		}
	}
}
