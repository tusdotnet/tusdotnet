using System.Threading;
using Shouldly;
using tusdotnet.Helpers;
using Xunit;

namespace tusdotnet.test.Tests
{
	public class FileLockTests
	{
		[Fact]
		public void LockAsync_Can_Lock_Successfully()
		{
			const string fileId = "testfile1";
			var fileLock1 = new FileLock(fileId);

			(fileLock1.Lock(CancellationToken.None)).ShouldBeTrue();

			var fileLock2 = new FileLock(fileId);
			(fileLock2.Lock(CancellationToken.None)).ShouldBeFalse();

			(fileLock1.Lock(CancellationToken.None)).ShouldBeTrue();

			fileLock1.ReleaseIfHeld();

			(fileLock2.Lock(CancellationToken.None)).ShouldBeTrue();

		}

		[Fact]
		public void ReleaseIfHeld_Relases_Lock_Successfully()
		{
			const string fileId = "testfile2";
			var fileLock1 = new FileLock(fileId);

			(fileLock1.Lock(CancellationToken.None)).ShouldBeTrue();

			var fileLock2 = new FileLock(fileId);
			(fileLock2.Lock(CancellationToken.None)).ShouldBeFalse();

			(fileLock1.Lock(CancellationToken.None)).ShouldBeTrue();

			fileLock1.ReleaseIfHeld();

			(fileLock2.Lock(CancellationToken.None)).ShouldBeTrue();
		}

		[Fact]
		public void ReleaseIfHeld_Does_Nothing_If_Lock_Was_Not_Held()
		{
			const string fileId = "testfile3";
			var fileLock1 = new FileLock(fileId);

			(fileLock1.Lock(CancellationToken.None)).ShouldBeTrue();

			var fileLock2 = new FileLock(fileId);
			(fileLock2.Lock(CancellationToken.None)).ShouldBeFalse();

			fileLock2.ReleaseIfHeld();

			var fileLock3 = new FileLock(fileId);
			fileLock3.ReleaseIfHeld();
			(fileLock3.Lock(CancellationToken.None)).ShouldBeFalse();
		}
	}
}
