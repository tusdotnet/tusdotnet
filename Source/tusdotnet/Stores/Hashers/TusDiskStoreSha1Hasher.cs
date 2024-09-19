#if !pipelines && !NETSTANDARD1_3

using System.Security.Cryptography;

namespace tusdotnet.Stores.Hashers
{
    internal class TusDiskStoreSha1Hasher : TusDiskStoreHasher
    {
        private readonly SHA1 _hash;

        public TusDiskStoreSha1Hasher()
        {
            _hash = SHA1.Create();
        }

        public override void Append(byte[] data, int count)
        {
            _hash.TransformBlock(data, 0, count, null, 0);
        }

        public override void Dispose()
        {
            _hash.Dispose();
        }

        public override byte[] GetHashAndReset()
        {
            _hash.TransformFinalBlock([], 0, 0);

            var value = _hash.Hash;

            _hash.Clear();

            return value;
        }
    }
}

#endif