using System.Threading.Tasks;
using Shouldly;
using tusdotnet.FileLocks;
using Xunit;

namespace tusdotnet.test.Tests
{
    public class InMemoryFileLockTests
    {
        [Fact]
        public async Task LockAsync_Can_Lock_Successfully()
        {
            const string fileId = "testfile1";
            var fileLock1 = new InMemoryFileLock(fileId);

            (await fileLock1.Lock()).ShouldBeTrue();

            var fileLock2 = new InMemoryFileLock(fileId);
            (await fileLock2.Lock()).ShouldBeFalse();

            (await fileLock1.Lock()).ShouldBeTrue();

            await fileLock1.ReleaseIfHeld();

            (await fileLock2.Lock()).ShouldBeTrue();
        }

        [Fact]
        public async Task ReleaseIfHeld_Relases_Lock_Successfully()
        {
            const string fileId = "testfile2";
            var fileLock1 = new InMemoryFileLock(fileId);

            (await fileLock1.Lock()).ShouldBeTrue();

            var fileLock2 = new InMemoryFileLock(fileId);
            (await fileLock2.Lock()).ShouldBeFalse();

            (await fileLock1.Lock()).ShouldBeTrue();

            await fileLock1.ReleaseIfHeld();

            (await fileLock2.Lock()).ShouldBeTrue();
        }

        [Fact]
        public async Task ReleaseIfHeld_Does_Nothing_If_Lock_Was_Not_Held()
        {
            const string fileId = "testfile3";
            var fileLock1 = new InMemoryFileLock(fileId);

            (await fileLock1.Lock()).ShouldBeTrue();

            var fileLock2 = new InMemoryFileLock(fileId);
            (await fileLock2.Lock()).ShouldBeFalse();

            await fileLock2.ReleaseIfHeld();

            var fileLock3 = new InMemoryFileLock(fileId);
            await fileLock3.ReleaseIfHeld();
            (await fileLock3.Lock()).ShouldBeFalse();
        }
    }
}
