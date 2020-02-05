using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Parsers;

namespace tusdotnet.Stores
{
	/// <summary>
	/// Represents a file saved in the TusDiskStore data store
	/// </summary>
	public class TusDiskFile : ITusFile
	{
        private readonly InternalFileRep _data;
        private readonly InternalFileRep _metadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="TusDiskFile"/> class.
        /// </summary>
        /// <param name="data">The file representation of the data</param>
        /// <param name="metadata">The file representation of the metadata</param>
        internal TusDiskFile(InternalFileRep data, InternalFileRep metadata)
		{
            Id = data.FileId;
            _data = data;
            _metadata = metadata;
		}

		/// <inheritdoc />
		public string Id { get; }

		/// <inheritdoc />
		public Task<Stream> GetContentAsync(CancellationToken cancellationToken)
		{
            return Task.FromResult<Stream>(_data.GetStream(FileMode.Open, FileAccess.Read, FileShare.Read));
		}

		/// <inheritdoc />
		public Task<Dictionary<string, Metadata>> GetMetadataAsync(CancellationToken cancellationToken)
		{
			var data = _metadata.ReadFirstLine(fileIsOptional: true);

			var parsedMetadata = MetadataParser.ParseAndValidate(MetadataParsingStrategy.AllowEmptyValues, data);

			return Task.FromResult(parsedMetadata.Metadata);
		}
	}
}
