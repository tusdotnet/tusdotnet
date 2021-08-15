using System;
using System.Threading.Tasks;
using tusdotnet.Interfaces;

namespace tusdotnet.Stores.FileIdProviders
{
    /// <summary>
    /// Provides file ids using GUIDs
    /// </summary>
    public class GuidFileIdProvider : ITusFileIdProvider
    {
        private readonly string _guildFormat;

        /// <summary>
        /// Creates a new TusGuildProvider
        /// </summary>
        /// <param name="guildFormat">The format of the guid to use when creating IDs</param>
        public GuidFileIdProvider(string guildFormat = "n")
        {
            _guildFormat = guildFormat;
        }

        /// <inheritdoc />
        public virtual Task<string> CreateId(string metadata)
        {
            return Task.FromResult(Guid.NewGuid().ToString(_guildFormat));
        }

        /// <inheritdoc />
        public virtual Task<bool> ValidateId(string fileId)
        {
            return Task.FromResult(Guid.TryParseExact(fileId, _guildFormat, out var _));
        }
    }
}
