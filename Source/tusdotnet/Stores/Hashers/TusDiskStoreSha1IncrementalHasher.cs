#if pipelines

using System.Buffers;
using System.Security.Cryptography;

namespace tusdotnet.Stores.Hashers
{
    internal class TusDiskStoreSha1IncrementalHasher : TusDiskStoreHasher
    {
        private readonly IncrementalHash _hash;

        public TusDiskStoreSha1IncrementalHasher()
        {
            _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        }

        public override void Append(ReadOnlySequence<byte> data)
        {
            foreach (var item in data)
            {
                _hash.AppendData(item.Span);
            }
        }

        public override void Append(byte[] data, int count)
        {
            _hash.AppendData(data, 0, count);
        }

        public override void Dispose()
        {
            _hash.Dispose();
        }

        public override byte[] GetHashAndReset()
        {
            return _hash.GetHashAndReset();
        }
    }
}

#endif