using System;
using System.IO;

namespace tusdotnet.Models
{
    internal abstract class ReadOnlyStream : Stream
    {
        protected Stream BackingStream { get; }

        protected ReadOnlyStream(Stream backingStream)
        {
            BackingStream = backingStream;
        }

        public override bool CanRead => BackingStream.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => BackingStream.Length;

        public override long Position { get => BackingStream.Position; set => throw new NotSupportedException(); }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
