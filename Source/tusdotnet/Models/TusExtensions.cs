using System.Collections.Generic;
using System.Linq;
using tusdotnet.Constants;

namespace tusdotnet.Models
{
    /// <summary>
    /// Tus extensions to use in <see cref="DefaultTusConfiguration"/>.
    /// </summary>
    public class TusExtensions
    {
        private readonly ISet<string> _extensionNamesInUse;

        private static readonly TusExtensions _all = new(
            ExtensionConstants.Creation,
            ExtensionConstants.CreationDeferLength,
            ExtensionConstants.CreationWithUpload,
            ExtensionConstants.Termination,
            ExtensionConstants.Checksum,
            ExtensionConstants.ChecksumTrailer,
            ExtensionConstants.Concatenation,
            ExtensionConstants.Expiration
        );

        private static readonly TusExtensions _none = new();

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="extensionNames">The names of the extensions (see <from cref="ExtensionConstants"/>) to allow</param>
        public TusExtensions(params string[] extensionNames)
        {
            _extensionNamesInUse = new HashSet<string>(extensionNames);
        }

        private TusExtensions(ISet<string> extensionNames)
        {
            _extensionNamesInUse = extensionNames;
        }

        /// <summary>
        /// All extensions allowed.
        /// </summary>
        public static TusExtensions All => _all;

        /// <summary>
        /// No extensions allowed.
        /// </summary>
        public static TusExtensions None => _none;

        /// <summary>
        /// Returns a list of extension names that are disabled.
        /// </summary>
        internal IEnumerable<string> Disallowed => All._extensionNamesInUse.Except(_extensionNamesInUse);

        /// <summary>
        /// Disable extensions based on the extension's name (use <see cref="ExtensionConstants"/> as source of names).
        /// </summary>
        /// <param name="extensionNames">The names to disable</param>
        /// <returns>A new <see cref="TusExtensions"/> object with the provided extension names disabled</returns>
        public TusExtensions Except(params string[] extensionNames)
        {
            var copy = new HashSet<string>(_extensionNamesInUse);
            foreach (var item in extensionNames)
            {
                copy.Remove(item);
            }

            return new TusExtensions(copy);
        }
    }
}
