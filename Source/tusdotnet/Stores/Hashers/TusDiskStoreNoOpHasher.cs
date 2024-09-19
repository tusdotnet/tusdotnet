#if pipelines
using System.Buffers;
#endif

namespace tusdotnet.Stores.Hashers
{
    internal abstract partial class TusDiskStoreHasher
    {
        private class TusDiskStoreNoOpHasher : TusDiskStoreHasher
        {
#if pipelines
            public override void Append(ReadOnlySequence<byte> data)
            {
            }
#endif
            public override void Append(byte[] data, int count)
            {
            }

            public override void Dispose()
            {
            }

            public override byte[] GetHashAndReset()
            {
                return null;
            }
        }
    }
}