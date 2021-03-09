using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using tusdotnet.Helpers;

namespace tusdotnet.Stores.BufferHandlers
{
    internal class NonconsecutiveMemoryWriteBuffer
    {
        private readonly LinkedList<Bucket> _buckets;
        private LinkedListNode<Bucket> _currentBucket;
        private int _currentBucketIndex;
        private readonly Stream _destination;
        private readonly int _numberOfBuckets;
        private readonly int _desiredBucketSizeInBytes;
        private readonly ArrayPool<byte> _pool;
        private readonly Func<Stream, Bucket, Task> _writeOperation;

        public NonconsecutiveMemoryWriteBuffer(bool useAsynchronousCodeFlow, int bufferSize, int maxBucketSize, ArrayPool<byte> pool, Stream destination)
        {
            _pool = pool;

            _numberOfBuckets = (int)Math.Ceiling(bufferSize / (double)maxBucketSize);
            _buckets = new LinkedList<Bucket>();

            _desiredBucketSizeInBytes = maxBucketSize;

            _buckets.AddLast(CreateBucket());

            _destination = destination;
            _currentBucketIndex = 0;
            _currentBucket = _buckets.First;

            if(useAsynchronousCodeFlow)
            {
                _writeOperation = (stream, bucket) => stream.WriteAsync(bucket.Data, 0, bucket.Position);
            }
            else
            {
                _writeOperation = (stream, bucket) =>
                {
                    stream.Write(bucket.Data, 0, bucket.Position);
                    return TaskHelper.Completed;
                };
            }
        }

        public async Task Append(byte[] buffer, int lengthInBufferToAppend)
        {
            // Everything fits into current bucket
            if (lengthInBufferToAppend <= _currentBucket.Value.RemainingBytes)
            {
                AppendToCurrentBucket(buffer, 0, lengthInBufferToAppend);
            }
            // Write what fits and then move to the next bucket
            else
            {
                var whatFits = _currentBucket.Value.RemainingBytes;
                AppendToCurrentBucket(buffer, 0, whatFits);

                // If we cannot move to the next bucket we write to the destination stream
                if (!MoveToNextBucket())
                {
                    await FlushAndResetBuckets();
                }

                // And append what is left to the current bucket.
                var remaining = lengthInBufferToAppend - whatFits;
                AppendToCurrentBucket(buffer, whatFits, remaining);
            }
        }

        private void AppendToCurrentBucket(byte[] data, int sourceIndex, int length)
        {
            if (length == 0) return;

            Array.Copy(
                sourceArray: data,
                sourceIndex: sourceIndex,
                destinationArray: _currentBucket.Value.Data,
                destinationIndex: _currentBucket.Value.Position,
                length: length);

            _currentBucket.Value.AdvancePosition(length);
        }

        private async Task FlushAndResetBuckets()
        {
            for (var node = _buckets.First; node != null; node = node.Next)
            {
                var bucket = node.Value;
                await _writeOperation(_destination, bucket);

                bucket.MarkAsWritten();
            }

            await _destination.FlushAsync();

            _currentBucket = _buckets.First;
            _currentBucketIndex = 0;
        }

        private bool MoveToNextBucket()
        {
            if (_currentBucketIndex >= _numberOfBuckets - 1)
            {
                // No more buckets are available
                return false;
            }

            // Add bucket if we do not have enough allocated.
            if (_buckets.Count < _numberOfBuckets)
            {
                _buckets.AddLast(CreateBucket());
            }

            _currentBucketIndex++;
            _currentBucket = _currentBucket.Next;
            return true;
        }

        private Bucket CreateBucket()
        {
            return new Bucket(_pool, _desiredBucketSizeInBytes);
        }

        internal Task FlushRemaining()
        {
            return FlushAndResetBuckets();
        }

        private class Bucket
        {
            public byte[] Data => _data ??= _pool.Rent(_desiredBucketSizeInBytes);

            public int Position { get; private set; }

            public int RemainingBytes => _data == null ? 0 : Data.Length - Position;

            private readonly ArrayPool<byte> _pool;
            private readonly int _desiredBucketSizeInBytes;
            private byte[] _data;

            public Bucket(ArrayPool<byte> pool, int desiredBucketSizeInBytes)
            {
                Position = 0;
                _pool = pool;
                _desiredBucketSizeInBytes = desiredBucketSizeInBytes;
            }

            public void AdvancePosition(int numberOfBytesToAdvance)
            {
                Position += numberOfBytesToAdvance;
            }

            public void MarkAsWritten()
            {
                _pool.Return(_data);
                Position = 0;
            }
        }
    }
}
