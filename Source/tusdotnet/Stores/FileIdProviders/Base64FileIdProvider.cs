using System;
using System.Threading.Tasks;
using System.Security.Cryptography;
using tusdotnet.Interfaces;

namespace tusdotnet.Stores.FileIdProviders
{
    /// <summary>
    /// Provides file ids using a random secure url safe base64 string
    /// </summary>
    public class Base64FileIdProvider : ITusFileIdProvider
    {
        private readonly int _byteLength;
        private readonly int _idLength;

        /// <summary>
        /// Creates a new TusBase64IdProvider
        /// The recommended byte length is 16.
        /// If you want an id similar to youtube, use a byte length of 8.
        /// </summary>
        /// <param name="byteLength">The amount of random bytes to encode to a base64 id</param>
        public Base64FileIdProvider(int byteLength = 16)
        {
            _byteLength = byteLength;

            var numWholeOrPartialInputBlocks = checked(byteLength + 2) / 3;
            _idLength = checked(numWholeOrPartialInputBlocks * 4);
        }

        /// <inheritdoc />
        public virtual Task<string> CreateId(string metadata)
        {
            var key = new byte[_byteLength];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key);
            }

            char[] output = new char[_idLength];

            // Start with default Base64 encoding.
            var numBase64Chars = Convert.ToBase64CharArray(key, 0, key.Length, output, 0);

            // Fix up '+' -> '-' and '/' -> '_'. Drop padding characters.
            var i = 0;
            for (; i < numBase64Chars; i++)
            {
                var ch = output[i];
                if (ch == '+')
                {
                    output[i] = '-';
                }
                else if (ch == '/')
                {
                    output[i] = '_';
                }
                else if (ch == '=')
                {
                    // We've reached a padding character; truncate the remainder.
                    break;
                }
            }

            return Task.FromResult(new string(output, 0, i));
        }

        /// <inheritdoc />
        public virtual Task<bool> ValidateId(string fileId)
        {
            // add length of padding chars
            int realIdLength;
            switch (fileId.Length % 4)
            {
                case 0:
                    realIdLength = fileId.Length + 0;
                    break;
                case 2:
                    realIdLength = fileId.Length + 2;
                    break;
                case 3:
                    realIdLength = fileId.Length + 1;
                    break;
                default:
                    return Task.FromResult(false);
            }

            if (realIdLength != _idLength) return Task.FromResult(false);
            return Task.FromResult(true);
        }
    }
}
