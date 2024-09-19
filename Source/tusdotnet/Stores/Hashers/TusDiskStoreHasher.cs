using System;
#if pipelines
using System.Buffers;
#endif

namespace tusdotnet.Stores.Hashers
{
    internal abstract partial class TusDiskStoreHasher : IDisposable
    {
        public static TusDiskStoreHasher Create(string algorithm)
        {
            if (algorithm?.Equals("sha1", StringComparison.OrdinalIgnoreCase) == true)
            {
#if pipelines
                return new TusDiskStoreSha1IncrementalHasher();

#elif !pipelines && !NETSTANDARD1_3

                return new TusDiskStoreSha1Hasher();
#endif
            }

            return new TusDiskStoreNoOpHasher();
        }

#if pipelines
        public abstract void Append(ReadOnlySequence<byte> data);
#endif

        public abstract void Append(byte[] data, int count);

        public abstract byte[] GetHashAndReset();

        public abstract void Dispose();
    }
}