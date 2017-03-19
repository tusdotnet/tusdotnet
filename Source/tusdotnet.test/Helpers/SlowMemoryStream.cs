using System.IO;
using System.Threading;

namespace tusdotnet.test.Helpers
{
	/// <summary>
	/// The SlowMemoryStream adds a 100 ms delay on every read.
	/// </summary>
	public class SlowMemoryStream : MemoryStream
	{
		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="buffer">The buffer to read from</param>
		public SlowMemoryStream(byte[] buffer) : base(buffer)
		{
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			Thread.Sleep(100);
			return base.Read(buffer, offset, count);
		}
	}
}
