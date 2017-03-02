using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Stores;
using Xunit;

namespace tusdotnet.test.Tests
{
	public class TusDiskStoreTests : IClassFixture<TusDiskStoreFixture>, IDisposable
	{
		private readonly TusDiskStoreFixture _fixture;

		public TusDiskStoreTests(TusDiskStoreFixture fixture)
		{
			_fixture = fixture;
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
				var exist = await _fixture.Store.FileExistAsync(Guid.NewGuid().ToString(), CancellationToken.None);
				exist.ShouldBeFalse();
			}

		}

		[Fact]
		public async Task GetUploadLengthAsync()
		{
			var fileId = await _fixture.Store.CreateFileAsync(3000, null, CancellationToken.None);
			var length = await _fixture.Store.GetUploadLengthAsync(fileId, CancellationToken.None);
			length.ShouldBe(3000);

			length = await _fixture.Store.GetUploadLengthAsync(Guid.NewGuid().ToString(), CancellationToken.None);
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

#warning This test fails from time to time due to timing issues (no pun). TODO Re-write to use a stream that reads slower so that we don't have to guess like below.


			var cancellationToken = new CancellationTokenSource();

			// Test cancellation.

			// 100 MB
			const int fileSize = 100 * 1024 * 1024;
			var fileId = await _fixture.Store.CreateFileAsync(fileSize, null, cancellationToken.Token);

			var buffer = new MemoryStream(new byte[fileSize]);

			var appendTask = Task.Run(() => _fixture.Store
				.AppendDataAsync(fileId, buffer, cancellationToken.Token), CancellationToken.None);
			await Task.Delay(10, CancellationToken.None);
			cancellationToken.Cancel();
			long bytesWritten = -1;

			try
			{
				bytesWritten = await appendTask;
				// Should have written something but should not have completed.
				bytesWritten.ShouldBeInRange(1, fileSize - 1);
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
				fileOffset.ShouldBeInRange(1, fileSize - 1);
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
			var file = await _fixture.Store.GetFileAsync(Guid.NewGuid().ToString(), CancellationToken.None);
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
		public async Task VerifyChecksumAsync_Data_Is_Truncated_If_Verification_Fails()
		{
			// Checksum is for "hello world"
			const string incorrectChecksum = "Kq5sNclPz7QV2+lfQIuc6R7oRu0=";
			const string message = "Hello World 12345!!@@åäö";

			var buffer = new UTF8Encoding(false).GetBytes(message);

			// Test complete upload
			var fileId = await _fixture.Store.CreateFileAsync(buffer.Length, null, CancellationToken.None);
			using (var stream = new MemoryStream(buffer))
			{
				await _fixture.Store.AppendDataAsync(fileId, stream, CancellationToken.None);
			}

			var checksumOk = await _fixture.Store
				.VerifyChecksumAsync(fileId, "sha1", Convert.FromBase64String(incorrectChecksum), CancellationToken.None);

			// File should not have been saved.
			checksumOk.ShouldBeFalse();
			var filePath = Path.Combine(_fixture.Path, fileId);
			new FileInfo(filePath).Length.ShouldBe(0);

			// Test chunked upload
			fileId = await _fixture.Store.CreateFileAsync(buffer.Length, null, CancellationToken.None);
			using (var stream = new MemoryStream(buffer.Take(10).ToArray()))
			{
				// Write first 10 bytes
				await _fixture.Store.AppendDataAsync(fileId, stream, CancellationToken.None);
			}

			using (var stream = new MemoryStream(buffer.Skip(10).ToArray()))
			{
				// Skip first 10 bytes and write the rest
				await _fixture.Store.AppendDataAsync(fileId, stream, CancellationToken.None);
			}

			checksumOk = await _fixture.Store.VerifyChecksumAsync(fileId, "sha1", Convert.FromBase64String(incorrectChecksum),
				CancellationToken.None);

			// Only first chunk should have been saved.
			checksumOk.ShouldBeFalse();
			filePath = Path.Combine(_fixture.Path, fileId);
			new FileInfo(filePath).Length.ShouldBe(10);
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
			await _fixture.Store.AppendDataAsync(p1, new MemoryStream(new byte[] { 0 }), CancellationToken.None);

			var exception =
				await Should.ThrowAsync<TusStoreException>(
					async () =>
						await _fixture.Store.CreateFinalFileAsync(new[] { p1, "nonexistingfileid" }, null, CancellationToken.None));

			exception.Message.ShouldBe("File nonexistingfileid does not exist");

		}

		public void Dispose()
		{
			_fixture.ClearPath();
		}
	}

	public class TusDiskStoreFixture : IDisposable
	{
		public string Path { get; set; }
		public TusDiskStore Store { get; set; }

		public TusDiskStoreFixture()
		{
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetTempFileName().Replace(".", ""));
			ClearPath();

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
