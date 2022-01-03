using System.Collections.Generic;
using System.Linq;
using tusdotnet.Constants;

namespace tusdotnet.Models
{
    public class TusExtensions
    {
        private readonly ISet<string> _extensionNames;
        private static readonly HashSet<string> _all = new()
        {
            ExtensionConstants.Creation,
            ExtensionConstants.CreationDeferLength,
            ExtensionConstants.CreationWithUpload,
            ExtensionConstants.Termination,
            ExtensionConstants.Checksum,
            ExtensionConstants.ChecksumTrailer,
            ExtensionConstants.Concatenation,
            ExtensionConstants.Expiration
        };

    public IEnumerable<string> Disallowed => All._extensionNames.Except(_extensionNames);

        public TusExtensions(ISet<string> extensionNames)
        {
            _extensionNames = extensionNames;
        }

        public TusExtensions(params string[] extensionNames)
        {
            _extensionNames = new HashSet<string>(extensionNames);
        }

        public static TusExtensions All
        {
            get
            {
                return new TusExtensions(_all);
            }
        }

        public TusExtensions Except(params string[] extensionNames)
        {
            var copy = new HashSet<string>(_extensionNames);
            foreach (var item in extensionNames)
            {
                copy.Remove(item);
            }

            return new TusExtensions(copy);
        }
    }
}
