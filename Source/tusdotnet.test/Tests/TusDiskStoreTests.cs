using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using tusdotnet.Helpers;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Stores;
using tusdotnet.test.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace tusdotnet.test.Tests
{
    public class TusDiskStoreTests : IClassFixture<TusDiskStoreFixture>, IDisposable
    {
        private readonly TusDiskStoreFixture _fixture;
        private readonly ITestOutputHelper _output;

        public TusDiskStoreTests(TusDiskStoreFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task CreateFileAsync()
        {
            for (var i = 0; i < 10; i++)
            {
                var fileId = await _fixture.Store.CreateFileAsync(i, null, CancellationToken.None);
                var filePath = Path.Combine(_fixture.Path, fileId);
                File.Exists(filePath).ShouldBeTrue();
            }
        }

        [Fact]
        public async Task FileExistsAsync()
        {
            for (var i = 0; i < 10; i++)
            {
                var fileId = await _fixture.Store.CreateFileAsync(i, null, CancellationToken.None);
                var exist = await _fixture.Store.FileExistAsync(fileId, CancellationToken.None);
                exist.ShouldBeTrue();
            }

            for (var i = 0; i < 10; i++)
            {
                var exist = await _fixture.Store.FileExistAsync(Guid.NewGuid().ToString("n"), CancellationToken.None);
                exist.ShouldBeFalse();
            }
        }

        [Fact]
        public async Task GetUploadLengthAsync()
        {
            var fileId = await _fixture.Store.CreateFileAsync(3000, null, CancellationToken.None);
            var length = await _fixture.Store.GetUploadLengthAsync(fileId, CancellationToken.None);
            length.ShouldBe(3000);

            length = await _fixture.Store.GetUploadLengthAsync(Guid.NewGuid().ToString("n"), CancellationToken.None);
            length.ShouldBeNull();

            File.Delete(Path.Combine(_fixture.Path, fileId + ".uploadlength"));

            length = await _fixture.Store.GetUploadLengthAsync(fileId, CancellationToken.None);
            length.ShouldBeNull();

            File.Create(Path.Combine(_fixture.Path, fileId + ".uploadlength")).Dispose();

            length = await _fixture.Store.GetUploadLengthAsync(fileId, CancellationToken.None);
            length.ShouldBeNull();
        }

        [Fact]
        public async Task GetUploadOffsetAsync()
        {
            var fileId = await _fixture.Store.CreateFileAsync(100, null, CancellationToken.None);

            var stream = new MemoryStream(new UTF8Encoding(false).GetBytes("Test content"));
            var bytesWritten = await _fixture.Store.AppendDataAsync(fileId, stream, CancellationToken.None);
            bytesWritten.ShouldBe(stream.Length);

            var offset = await _fixture.Store.GetUploadOffsetAsync(fileId, CancellationToken.None);
            offset.ShouldBe(bytesWritten);
        }

        [Fact]
        public async Task AppendDataAsync_Supports_Cancellation()
        {
            var cancellationToken = new CancellationTokenSource();

            // Test cancellation.

            // 5 MB
            const int fileSize = 5 * 1024 * 1024;
            var fileId = await _fixture.Store.CreateFileAsync(fileSize, null, cancellationToken.Token);

            var buffer = new SlowMemoryStream(new byte[fileSize]);

            var appendTask = _fixture.Store
                .AppendDataAsync(fileId, buffer, cancellationToken.Token);

            await Task.Delay(150, CancellationToken.None);

            cancellationToken.Cancel();

            long bytesWritten = -1;

            try
            {
                bytesWritten = await appendTask;
                // Should have written something but should not have completed.
                bytesWritten.ShouldBeInRange(1, 10240000);
            }
            catch (TaskCanceledException)
            {
                // The Owin test server throws this exception instead of just disconnecting the client.
                // If this happens just ignore the error and verify that the file has been written properly below.
            }

            var fileOffset = await _fixture.Store.GetUploadOffsetAsync(fileId, CancellationToken.None);
            if (bytesWritten != -1)
            {
                fileOffset.ShouldBe(bytesWritten);
            }
            else
            {
                fileOffset.ShouldBeInRange(1, 10240000);
            }

            var fileOnDiskSize = new FileInfo(Path.Combine(_fixture.Path, fileId)).Length;
            fileOnDiskSize.ShouldBe(fileOffset);
        }

        [Fact]
        public async Task AppendDataAsync_Throws_Exception_If_More_Data_Than_Upload_Length_Is_Provided()
        {
            // Test that it does not allow more than upload length to be written.

            var fileId = await _fixture.Store.CreateFileAsync(100, null, CancellationToken.None);

            var storeException = await Should.ThrowAsync<TusStoreException>(
                async () => await _fixture.Store.AppendDataAsync(fileId, new MemoryStream(new byte[101]), CancellationToken.None));

            storeException.Message.ShouldBe("Stream contains more data than the file's upload length. Stream data: 101, upload length: 100.");
        }

        [Fact]
        public async Task AppendDataAsync_Returns_Zero_If_File_Is_Already_Complete()
        {
            var fileId = await _fixture.Store.CreateFileAsync(100, null, CancellationToken.None);
            var length = await _fixture.Store.AppendDataAsync(fileId, new MemoryStream(new byte[100]), CancellationToken.None);
            length.ShouldBe(100);

            length = await _fixture.Store.AppendDataAsync(fileId, new MemoryStream(new byte[1]), CancellationToken.None);
            length.ShouldBe(0);
        }

        [Fact]
        public async Task GetFileAsync_Returns_File_If_The_File_Exist()
        {
            var fileId = await _fixture.Store.CreateFileAsync(100, null, CancellationToken.None);

            var content = Enumerable.Range(0, 100).Select(f => (byte)f).ToArray();

            await _fixture.Store.AppendDataAsync(fileId, new MemoryStream(content), CancellationToken.None);

            var file = await _fixture.Store.GetFileAsync(fileId, CancellationToken.None);

            file.Id.ShouldBe(fileId);

            using (var fileContent = await file.GetContentAsync(CancellationToken.None))
            {
                fileContent.Length.ShouldBe(content.Length);

                var fileContentBuffer = new byte[fileContent.Length];
                fileContent.Read(fileContentBuffer, 0, fileContentBuffer.Length);

                for (var i = 0; i < content.Length; i++)
                {
                    fileContentBuffer[i].ShouldBe(content[i]);
                }
            }
        }

        [Fact]
        public async Task GetFileAsync_Returns_Null_If_The_File_Does_Not_Exist()
        {
            var file = await _fixture.Store.GetFileAsync(Guid.NewGuid().ToString("n"), CancellationToken.None);
            file.ShouldBeNull();
        }

        [Fact]
        public async Task CreateFileAsync_Creates_Metadata_Properly()
        {
            var fileId = await _fixture.Store.CreateFileAsync(1, "key wrbDgMSaxafMsw==", CancellationToken.None);
            fileId.ShouldNotBeNull();

            var file = await _fixture.Store.GetFileAsync(fileId, CancellationToken.None);
            var metadata = await file.GetMetadataAsync(CancellationToken.None);
            metadata.ContainsKey("key").ShouldBeTrue();
            // Correct encoding
            metadata["key"].GetString(new UTF8Encoding()).ShouldBe("¶ÀĚŧ̳");
            // Wrong encoding just to test that the result is different.
            metadata["key"].GetString(new UTF7Encoding()).ShouldBe("Â¶ÃÄÅ§Ì³");
            metadata["key"].GetBytes().ShouldBe(new byte[] { 194, 182, 195, 128, 196, 154, 197, 167, 204, 179 });
        }

        [Fact]
        public async Task GetUploadMetadataAsync()
        {
            const string metadataConst = "key wrbDgMSaxafMsw==";
            var fileId = await _fixture.Store.CreateFileAsync(1, metadataConst, CancellationToken.None);

            var metadata = await _fixture.Store.GetUploadMetadataAsync(fileId, CancellationToken.None);
            metadata.ShouldBe(metadataConst);

            fileId = await _fixture.Store.CreateFileAsync(1, null, CancellationToken.None);
            metadata = await _fixture.Store.GetUploadMetadataAsync(fileId, CancellationToken.None);
            metadata.ShouldBeNull();

            fileId = await _fixture.Store.CreateFileAsync(1, "", CancellationToken.None);
            metadata = await _fixture.Store.GetUploadMetadataAsync(fileId, CancellationToken.None);
            metadata.ShouldBeNull();
        }

        [Fact]
        public async Task DeleteFileAsync()
        {
            const string metadataConst = "key wrbDgMSaxafMsw==";
            for (var i = 0; i < 10; i++)
            {
                var fileId = await _fixture.Store.CreateFileAsync(i + 1, i % 2 == 0 ? null : metadataConst, CancellationToken.None);
                var exist = await _fixture.Store.FileExistAsync(fileId, CancellationToken.None);
                exist.ShouldBeTrue();

                // Verify that all files exist.
                var filePath = Path.Combine(_fixture.Path, fileId);
                var uploadLengthPath = $"{filePath}.uploadlength";
                var metaPath = $"{filePath}.metadata";
                File.Exists(filePath).ShouldBeTrue();
                File.Exists(uploadLengthPath).ShouldBeTrue();
                File.Exists(metaPath).ShouldBeTrue();

                await _fixture.Store.DeleteFileAsync(fileId, CancellationToken.None);

                // Verify that all files were deleted.
                File.Exists(filePath).ShouldBeFalse();
                File.Exists(uploadLengthPath).ShouldBeFalse();
                File.Exists(metaPath).ShouldBeFalse();
            }
        }

        [Fact]
        public async Task GetSupportedAlgorithmsAsync()
        {
            var algorithms = await _fixture.Store.GetSupportedAlgorithmsAsync(CancellationToken.None);
            // ReSharper disable PossibleMultipleEnumeration
            algorithms.ShouldNotBeNull();
            algorithms.Count().ShouldBe(1);
            algorithms.First().ShouldBe("sha1");
            // ReSharper restore PossibleMultipleEnumeration
        }

        [Fact]
        public async Task VerifyChecksumAsync()
        {
            const string checksum = "9jSJuBxGMnq4UffwNYM8ct1tYQQ=";
            const string message = "Hello World 12345!!@@åäö";
            var buffer = new UTF8Encoding(false).GetBytes(message);

            var fileId = await _fixture.Store.CreateFileAsync(buffer.Length, null, CancellationToken.None);
            using (var stream = new MemoryStream(buffer))
            {
                await _fixture.Store.AppendDataAsync(fileId, stream, CancellationToken.None);
            }

            var checksumOk = await _fixture.Store.VerifyChecksumAsync(fileId, "sha1", Convert.FromBase64String(checksum),
                CancellationToken.None);

            checksumOk.ShouldBeTrue();
        }

        [Fact]
        public async Task VerifyChecksumAsync_Data_Is_Truncated_If_Verification_Fails_Using_Single_Chunk()
        {
            // Checksum is for "hello world"
            const string incorrectChecksum = "Kq5sNclPz7QV2+lfQIuc6R7oRu0=";
            const string message = "Hello World 12345!!@@åäö";

            var buffer = new UTF8Encoding(false).GetBytes(message);

            var bytesWritten = 0L;

            var fileId = await _fixture.Store.CreateFileAsync(buffer.Length, null, CancellationToken.None);

            // Emulate several requests
            do
            {
                // Create a new store and cancellation token source on each request as one would do in a real scenario.
                _fixture.CreateNewStore();
                var cts = new CancellationTokenSource();

                var requestStream = new RequestStreamFake(
                    (stream, bufferToFill, offset, count, ct) =>
                    {
                        // Emulate that the request completed
                        if (bytesWritten == buffer.Length)
                        {
                            return Task.FromResult(0);
                        }

                        // Emulate client disconnect after two bytes read.
                        if (stream.Position == 2)
                        {
                            cts.Cancel();
                            cts.Token.ThrowIfCancellationRequested();
                        }

                        bytesWritten += 2;
                        return stream.ReadBackingStreamAsync(bufferToFill, offset, 2, ct);
                    },
                    buffer.Skip((int)bytesWritten).ToArray());

                await ClientDisconnectGuard.ExecuteAsync(
                    () => _fixture.Store.AppendDataAsync(fileId, requestStream, cts.Token),
                    cts.Token);

            } while (bytesWritten < buffer.Length);

            var checksumOk = await _fixture.Store
                .VerifyChecksumAsync(fileId, "sha1", Convert.FromBase64String(incorrectChecksum), CancellationToken.None);

            // File should not have been saved.
            checksumOk.ShouldBeFalse();
            var filePath = Path.Combine(_fixture.Path, fileId);
            new FileInfo(filePath).Length.ShouldBe(0);
        }

        [Fact]
        public async Task VerifyChecksumAsync_Data_Is_Truncated_If_Verification_Fails_Using_Multiple_Chunks()
        {
            var chunks = new[]
            {
                new Tuple<string, string, bool>("Hello ", "lka6E6To6r7KT1JZv9faQdNooaY=", true),
                new Tuple<string, string, bool > ("World ", "eh6F0TXbUPbEiz7TtUFJ7WzNb9Q=", true),
                new Tuple<string, string, bool > ("12345!!@@åäö", "eh6F0TXbUPbEiz7TtUFJ7WzNb9Q=", false)
            };

            var encoding = new UTF8Encoding(false);
            var totalSize = 0;

            var fileId = await _fixture.Store.CreateFileAsync(chunks.Sum(f => encoding.GetBytes(f.Item1).Length), null, CancellationToken.None);

            foreach (var chunk in chunks)
            {
                var data = chunk.Item1;
                var checksum = chunk.Item2;
                var isValidChecksum = chunk.Item3;
                var dataBuffer = encoding.GetBytes(data);

                var bytesWritten = 0L;

                // Emulate several requests
                do
                {
                    // Create a new store and cancellation token source on each request as one would do in a real scenario.
                    _fixture.CreateNewStore();
                    var cts = new CancellationTokenSource();

                    var buffer = new RequestStreamFake(
                        (stream, bufferToFill, offset, count, ct) =>
                        {
                            // Emulate that the request completed
                            if (bytesWritten == dataBuffer.Length)
                            {
                                return Task.FromResult(0);
                            }

                            // Emulate client disconnect after two bytes read.
                            if (stream.Position == 2)
                            {
                                cts.Cancel();
                                cts.Token.ThrowIfCancellationRequested();
                            }

                            bytesWritten += 2;
                            return stream.ReadBackingStreamAsync(bufferToFill, offset, 2, ct);
                        },
                        dataBuffer.Skip((int)bytesWritten).ToArray());

                    await ClientDisconnectGuard.ExecuteAsync(
                        () => _fixture.Store.AppendDataAsync(fileId, buffer, cts.Token),
                        cts.Token);

                } while (bytesWritten < dataBuffer.Length);

                var checksumOk = await _fixture.Store.VerifyChecksumAsync(fileId, "sha1",
                    Convert.FromBase64String(checksum), CancellationToken.None);

                checksumOk.ShouldBe(isValidChecksum);

                totalSize += isValidChecksum ? dataBuffer.Length : 0;
            }

            var filePath = Path.Combine(_fixture.Path, fileId);
            new FileInfo(filePath).Length.ShouldBe(totalSize);
        }

        [Fact]
        public async Task CreatePartialFileAsync()
        {
            var fileId = await _fixture.Store.CreatePartialFileAsync(100, "key wrbDgMSaxafMsw==", CancellationToken.None);
            fileId.ShouldNotBeNullOrEmpty();
            var file = await _fixture.Store.GetFileAsync(fileId, CancellationToken.None);
            var metadata = await file.GetMetadataAsync(CancellationToken.None);
            metadata.ContainsKey("key").ShouldBeTrue();
            metadata["key"].GetString(new UTF8Encoding()).ShouldBe("¶ÀĚŧ̳");

            var uploadSize = await _fixture.Store.GetUploadLengthAsync(fileId, CancellationToken.None);
            uploadSize.ShouldBe(100);

            var uploadConcat = await _fixture.Store.GetUploadConcatAsync(fileId, CancellationToken.None);
            uploadConcat.ShouldBeOfType(typeof(FileConcatPartial));
        }

        [Fact]
        public async Task CreateFinalFileAsync()
        {
            // Create partial files
            var partial1Id = await _fixture.Store.CreatePartialFileAsync(100, null, CancellationToken.None);
            var partial2Id = await _fixture.Store.CreatePartialFileAsync(100, null, CancellationToken.None);

            await _fixture.Store.AppendDataAsync(partial1Id,
                new MemoryStream(Enumerable.Range(1, 100).Select(f => (byte)1).ToArray()), CancellationToken.None);
            await _fixture.Store.AppendDataAsync(partial2Id,
            new MemoryStream(Enumerable.Range(1, 100).Select(f => (byte)2).ToArray()), CancellationToken.None);

            // Create final file
            var finalFileId = await _fixture.Store.CreateFinalFileAsync(new[] { partial1Id, partial2Id }, null,
                CancellationToken.None);
            finalFileId.ShouldNotBeNullOrWhiteSpace();

            // Check file
            var finalConcat = await _fixture.Store.GetUploadConcatAsync(finalFileId, CancellationToken.None) as FileConcatFinal;
            finalConcat.ShouldNotBeNull();
            // ReSharper disable once PossibleNullReferenceException
            finalConcat.Files.Length.ShouldBe(2);
            finalConcat.Files[0].ShouldBe(partial1Id);
            finalConcat.Files[1].ShouldBe(partial2Id);

            var finalFile = await _fixture.Store.GetFileAsync(finalFileId, CancellationToken.None);
            finalFile.ShouldNotBeNull();
            var finalFileContentStream = await finalFile.GetContentAsync(CancellationToken.None);

            var buffer = new byte[finalFileContentStream.Length];
            using (var reader = new BinaryReader(finalFileContentStream))
            {
                reader.Read(buffer, 0, (int)finalFileContentStream.Length);
            }

            buffer.Length.ShouldBe(200);
            buffer.Take(100).ShouldAllBe(b => b == 1);
            buffer.Skip(100).ShouldAllBe(b => b == 2);
        }

        [Fact]
        public async Task CreateFinalFileAsync_Metadata_From_Partial_Files_Is_Not_Transferred_To_Final_File()
        {
            var partial1Id = await _fixture.Store.CreatePartialFileAsync(1, "key1 cGFydGlhbDFtZXRhZGF0YQ==",
                CancellationToken.None);
            var partial2Id = await _fixture.Store.CreatePartialFileAsync(1, "key2 bWV0YWRhdGFmb3JwYXJ0aWFsMg==",
                CancellationToken.None);

#pragma warning disable 4014
            _fixture.Store.AppendDataAsync(partial1Id, new MemoryStream(new byte[] { 1 }), CancellationToken.None);
            _fixture.Store.AppendDataAsync(partial2Id, new MemoryStream(new byte[] { 1 }), CancellationToken.None);
#pragma warning restore 4014

            // Create final file with no metadata
            var finalId = await _fixture.Store.CreateFinalFileAsync(new[] { partial1Id, partial2Id }, null, CancellationToken.None);

            var metadata = await _fixture.Store.GetUploadMetadataAsync(finalId, CancellationToken.None);
            metadata.ShouldBeNull();

            finalId = await _fixture.Store.CreateFinalFileAsync(new[] { partial1Id, partial2Id },
                "finalkey c29tZWZpbmFsbWV0YWRhdGE=", CancellationToken.None);

            metadata = await _fixture.Store.GetUploadMetadataAsync(finalId, CancellationToken.None);
            metadata.ShouldNotBeNullOrWhiteSpace();
            var parsedMetadata = Metadata.Parse(metadata);

            parsedMetadata.ContainsKey("finalkey").ShouldBeTrue();
            parsedMetadata["finalkey"].GetString(Encoding.UTF8).ShouldBe("somefinalmetadata");

            parsedMetadata.ContainsKey("key1").ShouldBeFalse();
            parsedMetadata.ContainsKey("key2").ShouldBeFalse();
        }

        [Fact]
        public async Task CreateFinalFileAsync_Deletes_Partial_Files_If_Configuration_Says_So()
        {
            // Use default constructor.
            var store = new TusDiskStore(_fixture.Path);

            var p1 = await store.CreatePartialFileAsync(1, null, CancellationToken.None);
            var p2 = await store.CreatePartialFileAsync(1, null, CancellationToken.None);

            await store.AppendDataAsync(p1, new MemoryStream(new byte[] { 1 }), CancellationToken.None);
            await store.AppendDataAsync(p2, new MemoryStream(new byte[] { 2 }), CancellationToken.None);

            var f = await store.CreateFinalFileAsync(new[] { p1, p2 }, null, CancellationToken.None);
            f.ShouldNotBeNullOrWhiteSpace();
            (await store.FileExistAsync(p1, CancellationToken.None)).ShouldBeTrue();
            (await store.FileExistAsync(p2, CancellationToken.None)).ShouldBeTrue();

            // Cleanup = true
            store = new TusDiskStore(_fixture.Path, true);

            p1 = await store.CreatePartialFileAsync(1, null, CancellationToken.None);
            p1.ShouldNotBeNullOrWhiteSpace();
            p2 = await store.CreatePartialFileAsync(1, null, CancellationToken.None);
            p2.ShouldNotBeNullOrWhiteSpace();

            await store.AppendDataAsync(p1, new MemoryStream(new byte[] { 1 }), CancellationToken.None);
            await store.AppendDataAsync(p2, new MemoryStream(new byte[] { 2 }), CancellationToken.None);

            f = await store.CreateFinalFileAsync(new[] { p1, p2 }, null, CancellationToken.None);
            f.ShouldNotBeNullOrWhiteSpace();
            (await store.FileExistAsync(p1, CancellationToken.None)).ShouldBeFalse();
            (await store.FileExistAsync(p2, CancellationToken.None)).ShouldBeFalse();

            // Cleanup = false
            store = new TusDiskStore(_fixture.Path, false);

            p1 = await store.CreatePartialFileAsync(1, null, CancellationToken.None);
            p2 = await store.CreatePartialFileAsync(1, null, CancellationToken.None);

            await store.AppendDataAsync(p1, new MemoryStream(new byte[] { 1 }), CancellationToken.None);
            await store.AppendDataAsync(p2, new MemoryStream(new byte[] { 2 }), CancellationToken.None);

            f = await store.CreateFinalFileAsync(new[] { p1, p2 }, null, CancellationToken.None);
            f.ShouldNotBeNullOrWhiteSpace();
            (await store.FileExistAsync(p1, CancellationToken.None)).ShouldBeTrue();
            (await store.FileExistAsync(p2, CancellationToken.None)).ShouldBeTrue();
        }

        [Fact]
        public async Task CreateFinalFileAsync_Throws_Exception_If_Any_Partial_File_Does_Not_Exist()
        {
            var p1 = await _fixture.Store.CreatePartialFileAsync(1, null, CancellationToken.None);
            var nonexistingfileid = Guid.NewGuid().ToString("n");
            await _fixture.Store.AppendDataAsync(p1, new MemoryStream(new byte[] { 0 }), CancellationToken.None);

            var exception =
                await Should.ThrowAsync<TusStoreException>(
                    async () =>
                        await _fixture.Store.CreateFinalFileAsync(new[] { p1, nonexistingfileid }, null, CancellationToken.None));

            exception.Message.ShouldBe($"File {nonexistingfileid} does not exist");
        }

        [Fact]
        public async Task SetExpirationAsync()
        {
            var file = await _fixture.Store.CreateFileAsync(1, null, CancellationToken.None);
            var expires = DateTimeOffset.UtcNow.AddSeconds(10);
            await _fixture.Store.SetExpirationAsync(file, expires, CancellationToken.None);

            File.Exists(Path.Combine(_fixture.Path, $"{file}.expiration")).ShouldBeTrue();
        }

        [Fact]
        public async Task GetExpirationAsync()
        {
            var file = await _fixture.Store.CreateFileAsync(1, null, CancellationToken.None);
            var expires = DateTimeOffset.UtcNow.AddSeconds(10);
            await _fixture.Store.SetExpirationAsync(file, expires, CancellationToken.None);

            File.Exists(Path.Combine(_fixture.Path, $"{file}.expiration")).ShouldBeTrue();

            var readExpires = await _fixture.Store.GetExpirationAsync(file, CancellationToken.None);
            readExpires.ShouldBe(expires);
        }

        [Fact]
        public async Task GetExpirationAsync_Returns_Null_If_No_Expiration_Has_Been_Set()
        {
            var file = await _fixture.Store.CreateFileAsync(1, null, CancellationToken.None);

            File.Exists(Path.Combine(_fixture.Path, $"{file}.expiration")).ShouldBeFalse();

            var readExpires = await _fixture.Store.GetExpirationAsync(file, CancellationToken.None);
            readExpires.ShouldBeNull();
        }

        [Fact]
        public async Task GetExpiredFilesAsync()
        {
            var file1 = await _fixture.Store.CreateFileAsync(1, null, CancellationToken.None);
            var expires = DateTimeOffset.UtcNow.AddSeconds(-10);
            await _fixture.Store.SetExpirationAsync(file1, expires, CancellationToken.None);

            var file2 = await _fixture.Store.CreateFileAsync(1, null, CancellationToken.None);
            expires = DateTimeOffset.UtcNow.AddSeconds(-10);
            await _fixture.Store.SetExpirationAsync(file2, expires, CancellationToken.None);

            var file3 = await _fixture.Store.CreateFileAsync(1, null, CancellationToken.None);
            expires = DateTimeOffset.UtcNow.AddSeconds(10);
            await _fixture.Store.SetExpirationAsync(file3, expires, CancellationToken.None);

            // Completed files should not be removed.
            var file4 = await _fixture.Store.CreateFileAsync(1, null, CancellationToken.None);
            await _fixture.Store.AppendDataAsync(file4, new MemoryStream(new byte[] { 1 }), CancellationToken.None);
            expires = DateTimeOffset.UtcNow.AddSeconds(-10);
            await _fixture.Store.SetExpirationAsync(file4, expires, CancellationToken.None);

            var expiredFiles = (await _fixture.Store.GetExpiredFilesAsync(CancellationToken.None)).ToList();
            expiredFiles.Count.ShouldBe(2);
            expiredFiles.Contains(file1).ShouldBeTrue();
            expiredFiles.Contains(file2).ShouldBeTrue();
        }

        [Fact]
        public async Task RemoveExpiredFilesAsync()
        {
            var file1 = await _fixture.Store.CreateFileAsync(1, null, CancellationToken.None);
            var expires = DateTimeOffset.UtcNow.AddSeconds(-10);
            await _fixture.Store.SetExpirationAsync(file1, expires, CancellationToken.None);

            var file2 = await _fixture.Store.CreateFileAsync(1, null, CancellationToken.None);
            expires = DateTimeOffset.UtcNow.AddSeconds(-10);
            await _fixture.Store.SetExpirationAsync(file2, expires, CancellationToken.None);

            var file3 = await _fixture.Store.CreateFileAsync(1, null, CancellationToken.None);
            expires = DateTimeOffset.UtcNow.AddSeconds(10);
            await _fixture.Store.SetExpirationAsync(file3, expires, CancellationToken.None);

            // Completed files should not be removed.
            var file4 = await _fixture.Store.CreateFileAsync(1, null, CancellationToken.None);
            await _fixture.Store.AppendDataAsync(file4, new MemoryStream(new byte[] { 1 }), CancellationToken.None);
            expires = DateTimeOffset.UtcNow.AddSeconds(-10);
            await _fixture.Store.SetExpirationAsync(file4, expires, CancellationToken.None);

            foreach (var file in new[] { file1, file2, file3, file4 })
            {
                AssertExpirationFileExist(file, true);
            }

            await _fixture.Store.RemoveExpiredFilesAsync(CancellationToken.None);

            AssertExpirationFileExist(file1, false);
            AssertExpirationFileExist(file2, false);
            AssertExpirationFileExist(file3, true);
            AssertExpirationFileExist(file4, true);

            void AssertExpirationFileExist(string fileId, bool shouldExist)
            {
                File.Exists(Path.Combine(_fixture.Path, $"{fileId}.expiration")).ShouldBe(shouldExist);
            }
        }

        [Fact]
        public async Task SetUploadLengthAsync()
        {
            var fileId = await _fixture.Store.CreateFileAsync(-1, null, CancellationToken.None);

            var uploadLength = await _fixture.Store.GetUploadLengthAsync(fileId, CancellationToken.None);
            uploadLength.ShouldBeNull();

            await _fixture.Store.SetUploadLengthAsync(fileId, 100, CancellationToken.None);

            uploadLength = await _fixture.Store.GetUploadLengthAsync(fileId, CancellationToken.None);
            uploadLength.ShouldBe(100);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped - This test method should ;) 
        [Fact(Skip = "No need to run it each time")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public async Task RemoveExpiredFilesAsync_PerformanceTest()
        {
            const int numberOfFilesToCreate = 100_000;

            var ids = new List<string>(numberOfFilesToCreate);
            for (var i = 0; i < numberOfFilesToCreate; i++)
            {
                var file = await _fixture.Store.CreateFileAsync(300, null, CancellationToken.None);
                await _fixture.Store.SetExpirationAsync(file,
                    i % 2 == 0
                        ? DateTimeOffset.MaxValue
                        : DateTimeOffset.UtcNow.AddSeconds(-1),
                    CancellationToken.None);
                ids.Add(file);
            }

            var watch = Stopwatch.StartNew();

            await _fixture.Store.RemoveExpiredFilesAsync(CancellationToken.None);

            watch.Stop();

            var removed = ids.Where(f => !_fixture.Store.FileExistAsync(f, CancellationToken.None).Result).ToList();

            _output.WriteLine($"Deleted {removed.Count} of {numberOfFilesToCreate} files in {watch.ElapsedMilliseconds} ms");
        }

        [Theory]
        [InlineData("..\file.txt", false)]
        [InlineData("..\\file.txt", false)]
        [InlineData("..\\..\\file.txt", false)]
        [InlineData("file.txt", false)]
        [InlineData("6938F0FF-9434-4F4D-8F9C-7A1E38EC9F7A", false)]
        [InlineData("", false)]
        [InlineData(" ", false)]
        [InlineData("\t", false)]
        [InlineData("\n", false)]
        [InlineData("6938f0ff94344f4d8f9c7a1e38ec9f7a", true)]
        public void FileId_Is_Validated_Before_Being_Used(string fileId, bool isValid)
        {
            // No need to test CreateFileAsync, CreatePartialFileAsync, GetExpiredFilesAsync, RemovedExpiredFilesAsync 
            // as these does not take a file id as a parameter

            var allAsserted = new List<Task<Exception>>(13)
            {
                AssertFileIdForMethod(() => _fixture.Store.AppendDataAsync(fileId, new MemoryStream(), CancellationToken.None)),
                AssertFileIdForMethod(() => _fixture.Store.CreateFinalFileAsync(new[] { fileId }, null, CancellationToken.None)),
                AssertFileIdForMethod(() => _fixture.Store.DeleteFileAsync(fileId, CancellationToken.None)),
                AssertFileIdForMethod(() => _fixture.Store.FileExistAsync(fileId, CancellationToken.None)),
                AssertFileIdForMethod(() => _fixture.Store.GetExpirationAsync(fileId, CancellationToken.None)),
                AssertFileIdForMethod(() => _fixture.Store.GetFileAsync(fileId, CancellationToken.None)),
                AssertFileIdForMethod(() => _fixture.Store.GetUploadConcatAsync(fileId, CancellationToken.None)),
                AssertFileIdForMethod(() => _fixture.Store.GetUploadLengthAsync(fileId, CancellationToken.None)),
                AssertFileIdForMethod(() => _fixture.Store.GetUploadMetadataAsync(fileId, CancellationToken.None)),
                AssertFileIdForMethod(() => _fixture.Store.GetUploadOffsetAsync(fileId, CancellationToken.None)),
                AssertFileIdForMethod(() => _fixture.Store.SetExpirationAsync(fileId, DateTimeOffset.MaxValue, CancellationToken.None)),
                AssertFileIdForMethod(() => _fixture.Store.SetUploadLengthAsync(fileId, 1, CancellationToken.None)),
                AssertFileIdForMethod(() => _fixture.Store.VerifyChecksumAsync(fileId, "sha1", new byte[] { 1 }, CancellationToken.None))
            };

            allAsserted.All(f => isValid ? f.Result == null : f.Result != null).ShouldBeTrue();

            async Task<Exception> AssertFileIdForMethod(Func<Task> callWrapper)
            {
                try
                {
                    await callWrapper();
                    return null;
                }
                catch (TusStoreException e)
                {
                    return e;
                }
                catch (Exception)
                {
                    // Ignore other exceptions
                    return null;
                }
            }
        }

        public void Dispose()
        {
            _fixture.ClearPath();
        }
    }

    // ReSharper disable once ClassNeverInstantiated.Global - Instantiated by xUnit.
    public class TusDiskStoreFixture : IDisposable
    {
        public string Path { get; }
        public TusDiskStore Store { get; private set; }

        public TusDiskStoreFixture()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetTempFileName().Replace(".", ""));
            ClearPath();

            CreateNewStore();
        }

        public void CreateNewStore()
        {
            Store = new TusDiskStore(Path);
        }

        public void Dispose()
        {
            Directory.Delete(Path, true);
        }

        public void ClearPath()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
            Directory.CreateDirectory(Path);
        }
    }
}
