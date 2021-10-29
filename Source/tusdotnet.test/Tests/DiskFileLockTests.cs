using Shouldly;
using System;
using System.IO;
using System.Threading.Tasks;
using tusdotnet.FileLocks;
using Xunit;

namespace tusdotnet.test.Tests
{
    public class DiskFileLockTests : IClassFixture<DiskFileLockTestsFixture>
    {
        private readonly DiskFileLockTestsFixture _fixture;

        public DiskFileLockTests(DiskFileLockTestsFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Lock_Can_Lock_Successfully()
        {
            const string fileId = "testfile1";
            var fileLock1 = GetFileLock(fileId);

            (await fileLock1.Lock()).ShouldBeTrue();

            var fileLock2 = GetFileLock(fileId);
            (await fileLock2.Lock()).ShouldBeFalse();

            (await fileLock1.Lock()).ShouldBeTrue();

            await fileLock1.ReleaseIfHeld();

            (await fileLock2.Lock()).ShouldBeTrue();
        }

        [Fact]
        public async Task ReleaseIfHeld_Relases_Lock_Successfully()
        {
            const string fileId = "testfile2";
            var fileLock1 = GetFileLock(fileId);

            (await fileLock1.Lock()).ShouldBeTrue();

            var fileLock2 = GetFileLock(fileId);
            (await fileLock2.Lock()).ShouldBeFalse();

            (await fileLock1.Lock()).ShouldBeTrue();

            await fileLock1.ReleaseIfHeld();

            (await fileLock2.Lock()).ShouldBeTrue();
        }

        [Fact]
        public async Task ReleaseIfHeld_Does_Nothing_If_Lock_Was_Not_Held()
        {
            const string fileId = "testfile3";
            var fileLock1 = GetFileLock(fileId);

            (await fileLock1.Lock()).ShouldBeTrue();

            var fileLock2 = GetFileLock(fileId);
            (await fileLock2.Lock()).ShouldBeFalse();

            await fileLock2.ReleaseIfHeld();

            var fileLock3 = GetFileLock(fileId);
            await fileLock3.ReleaseIfHeld();
            (await fileLock3.Lock()).ShouldBeFalse();
        }

        private DiskFileLock GetFileLock(string fileId)
        {
            return (DiskFileLock)_fixture.Provider.AquireLock(fileId).Result;
        }
    }

    public sealed class DiskFileLockTestsFixture : IDisposable
    {
        public DiskFileLockProvider Provider { get; set; }

        public DiskFileLockTestsFixture()
        {
            var diskPath = Path.Combine(Path.GetTempPath(), "tempfilelocks");
            if (!Directory.Exists(diskPath))
                Directory.CreateDirectory(diskPath);

            Provider = new(diskPath);
        }

        public void Dispose()
        {
            Directory.Delete(Path.Combine(Path.GetTempPath(), "tempfilelocks"), recursive: true);
        }
    }
}
