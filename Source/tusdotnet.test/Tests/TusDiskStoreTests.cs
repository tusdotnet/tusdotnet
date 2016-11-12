using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using tusdotnet.Models;
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
			var cancellationToken = new CancellationTokenSource();

			// Test cancellation.

			// 30 MB
			const int fileSize = 30 * 1024 * 1024;
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

			using (var fileContent = await file.GetContent(CancellationToken.None))
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
