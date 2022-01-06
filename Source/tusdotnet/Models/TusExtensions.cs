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
        private readonly HashSet<string> _extensionNamesInUse;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="extensions">The extensions to allow. All extensions are available as static properties on the <see cref="TusExtensions"/> class</param>
        public TusExtensions(params TusExtensions[] extensions)
        {
            _extensionNamesInUse = new HashSet<string>(extensions.SelectMany(x => x._extensionNamesInUse));
        }

        private TusExtensions(HashSet<string> extensionNames)
        {
            _extensionNamesInUse = extensionNames;
        }

        private TusExtensions(string extensionName)
        {
            _extensionNamesInUse = new HashSet<string>
            {
                extensionName
            };
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

        public static TusExtensions Checksum { get; } = new TusExtensions(ExtensionConstants.Checksum);

        public static TusExtensions ChecksumTrailer { get; } = new TusExtensions(ExtensionConstants.ChecksumTrailer);

        public static TusExtensions Concatenation { get; } = new TusExtensions(ExtensionConstants.Concatenation);

        public static TusExtensions Creation { get; } = new TusExtensions(ExtensionConstants.Creation);

        public static TusExtensions CreationDeferLength { get; } = new TusExtensions(ExtensionConstants.CreationDeferLength);

        public static TusExtensions CreationWithUpload { get; } = new TusExtensions(ExtensionConstants.CreationWithUpload);

        public static TusExtensions Expiration { get; } = new TusExtensions(ExtensionConstants.Expiration);

        public static TusExtensions Termination { get; } = new TusExtensions(ExtensionConstants.Termination);

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        /// <summary>
        /// All extensions allowed.
        /// </summary>
        public static TusExtensions All { get; } = new(
            Creation,
            CreationDeferLength,
            CreationWithUpload,
            Termination,
            Checksum,
            ChecksumTrailer,
            Concatenation,
            Expiration
        );

        /// <summary>
        /// No extensions allowed.
        /// </summary>
        public static TusExtensions None { get; } = new();

        /// <summary>
        /// Returns a list of extension names that are disabled.
        /// </summary>
        internal IEnumerable<string> Disallowed => All._extensionNamesInUse.Except(_extensionNamesInUse);

        /// <summary>
        /// Disable extensions based on the extension's name (use <see cref="ExtensionConstants"/> as source of names).
        /// </summary>
        /// <param name="extensions">The extensions to disable</param>
        /// <returns>A new <see cref="TusExtensions"/> object with the provided extension names disabled</returns>
        public TusExtensions Except(params TusExtensions[] extensions)
        {
            var copy = new HashSet<string>(_extensionNamesInUse);
            foreach (var item in extensions.SelectMany(x => x._extensionNamesInUse))
            {
                copy.Remove(item);
            }

            return new TusExtensions(copy);
        }
    }
}
