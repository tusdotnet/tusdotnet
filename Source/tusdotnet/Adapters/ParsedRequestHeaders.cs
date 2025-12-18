using System;
using System.Collections.Generic;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;

namespace tusdotnet.Adapters
{
    /// <summary>
    /// Container for parsed and validated request headers.
    /// Each property can only be set once during validation. Attempting to set a property
    /// more than once will throw an InvalidOperationException.
    /// </summary>
    internal class ParsedRequestHeaders
    {
        private Dictionary<string, Metadata> _metadata;
        private UploadConcat _uploadConcat;
        private Checksum _uploadChecksum;

        public Dictionary<string, Metadata> Metadata
        {
            get => _metadata ?? [];
            set => SetOnce(ref _metadata, value, nameof(Metadata));
        }

        public UploadConcat UploadConcat
        {
            get => _uploadConcat;
            set => SetOnce(ref _uploadConcat, value, nameof(UploadConcat));
        }

        public Checksum UploadChecksum
        {
            get => _uploadChecksum;
            set => SetOnce(ref _uploadChecksum, value, nameof(UploadChecksum));
        }

        private void SetOnce<T>(ref T field, T value, string propertyName)
            where T : class
        {
            if (field != null)
            {
                throw new InvalidOperationException(
                    $"{propertyName} has already been set and cannot be modified."
                );
            }
            field = value;
        }
    }
}
