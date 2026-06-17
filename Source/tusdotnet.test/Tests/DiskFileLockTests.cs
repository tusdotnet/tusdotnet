using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
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

        [Theory]
        [InlineData("../../../testfile_traversal", "testfile_traversal")]
        [InlineData("..\\..\\..\\testfile_traversal", "testfile_traversal")]
        [InlineData("subfolder/testfile_traversal", "testfile_traversal")]
        [InlineData("subfolder\\another\\file", "file")]
        [InlineData("/file", "file")]
        public async Task Lock_Sanitizes_Invalid_Paths_In_FileId(
            string maliciousId,
            string expectedFileName
        )
        {
            var uniqueId = Guid.NewGuid().ToString();
            maliciousId += uniqueId;
            expectedFileName += uniqueId;

            var maliciousLock = GetFileLock(maliciousId);
            // Lock should succeed because the malicious path gets sanitized
            (await maliciousLock.Lock()).ShouldBeTrue();

            // Try to lock with the sanitized filename - should fail because first lock has it
            var sanitizedLock = GetFileLock(expectedFileName);
            (await sanitizedLock.Lock()).ShouldBeFalse();

            // Release the malicious lock
            await maliciousLock.ReleaseIfHeld();

            // Now the sanitized one should succeed
            (await sanitizedLock.Lock()).ShouldBeTrue();
            await sanitizedLock.ReleaseIfHeld();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task Lock_Returns_False_For_Empty_FileId_After_Sanitization(string maliciousId)
        {
            var lockFolderLocation = Path.Combine(
                Path.GetTempPath(),
                "tempfilelocks_" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(lockFolderLocation);
            try
            {
                var fileLock = new DiskFileLock(lockFolderLocation, maliciousId);
                (await fileLock.Lock()).ShouldBeFalse();
            }
            finally
            {
                if (Directory.Exists(lockFolderLocation))
                {
                    Directory.Delete(lockFolderLocation, true);
                }
            }
        }

        private DiskFileLock GetFileLock(string fileId)
        {
            return (DiskFileLock)_fixture.Provider.AquireLock(fileId).Result;
        }
    }

    public sealed class DiskFileLockTestsFixture : IDisposable
    {
        public DiskFileLockProvider Provider { get; set; }
        private readonly string _diskPath;

        public DiskFileLockTestsFixture()
        {
            _diskPath = Path.Combine(
                Path.GetTempPath(),
                "tempfilelocks_" + Guid.NewGuid().ToString("N")
            );
            if (!Directory.Exists(_diskPath))
                Directory.CreateDirectory(_diskPath);

            Provider = new(_diskPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_diskPath))
            {
                Directory.Delete(_diskPath, recursive: true);
            }
        }
    }
}
