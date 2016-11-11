using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;

namespace tusdotnet.Stores
{
	public class TusDiskFile : ITusFile
	{
		private readonly string _filePath;

		internal TusDiskFile(string directoryPath, string fileId, string metadata)
        {
            Id = fileId;
            _filePath = Path.Combine(directoryPath, Id);
            Metadata = ParseMetadata(metadata);
        }

        private Dictionary<string, string> ParseMetadata(string metadata)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(metadata))
            {
                var metadataPairs = metadata.Split(',');
                foreach (var pair in metadataPairs)
                {
                    var keyAndValue = pair.Split(' ');
                    var key = keyAndValue[0];
                    var base64Value = keyAndValue[1];
                    var valueBytes = Convert.FromBase64String(base64Value);
                    var value = System.Text.Encoding.Default.GetString(valueBytes);

                    dictionary[key] = value;
                }
            }

            return dictionary;
        }


        internal bool Exist()
		{
			return File.Exists(_filePath);
		}

		public string Id { get; }
		public Task<Stream> GetContent(CancellationToken cancellationToken)
		{
			var stream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			return Task.FromResult<Stream>(stream);
		}

        public Dictionary<string, string> Metadata { get; }
	}
}
