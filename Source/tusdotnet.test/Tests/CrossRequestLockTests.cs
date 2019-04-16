using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.test.Extensions;
using Xunit;

namespace tusdotnet.test.Tests
{
    /// <summary>
    /// Tests that DELETE and PATCH requests for the same file cannot happen at the same time.
    /// </summary>
    public class CrossRequestLockTests
    {
        [Fact]
        public async Task Returns_409_Conflict_For_A_Patch_Request_If_A_Delete_Is_Ongoing()
        {
            var fileId = Guid.NewGuid().ToString();

            using (var server = TestServerFactory.Create(app =>
            {
                var store = Substitute.For<ITusStore, ITusTerminationStore>();
                store.FileExistAsync(fileId, Arg.Any<CancellationToken>()).Returns(true);
                ((ITusTerminationStore)store).DeleteFileAsync(fileId, Arg.Any<CancellationToken>()).Returns(_ =>
               {
                   Thread.Sleep(500);
                   return Task.FromResult(0);
               });

                app.UseTus(_ => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {
                var deleteRequest = server.CreateRequest($"/files/{fileId}")
                    .AddTusResumableHeader()
                    .SendAsync("DELETE");

                await Task.Delay(50);

                var patchRequest = server.CreateRequest($"/files/{fileId}")
                    .And(message =>
                    {
                        message.Content = new StreamContent(new MemoryStream(new byte[3]));
                        message.Content.Headers.ContentType =
                            new MediaTypeHeaderValue("application/offset+octet-stream");
                    })
                    .AddHeader("Upload-Offset", "0")
                    .AddTusResumableHeader()
                    .SendAsync("PATCH");

                await Task.WhenAll(deleteRequest, patchRequest);

                deleteRequest.Result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
                patchRequest.Result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            }
        }

        [Fact]
        public async Task Returns_409_Conflict_For_A_Delete_Request_If_A_Patch_Is_Ongoing()
        {
            var fileId = Guid.NewGuid().ToString();
            using (var server = TestServerFactory.Create(app =>
            {
                var store = Substitute.For<ITusStore, ITusTerminationStore>();
                store.FileExistAsync(fileId, Arg.Any<CancellationToken>()).Returns(true);
                store.GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>()).Returns(0);
                store.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(10);
                store.AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                    .Returns(_ =>
                    {
                        Thread.Sleep(5000);
                        return 3;
                    });
                ((ITusTerminationStore)store).DeleteFileAsync(fileId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(0));

                app.UseTus(_ => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {
                var patchRequest = server.CreateRequest($"/files/{fileId}")
                    .And(message =>
                    {
                        message.Content = new StreamContent(new MemoryStream(new byte[3]));
                        message.Content.Headers.ContentType =
                            new MediaTypeHeaderValue("application/offset+octet-stream");
                    })
                    .AddHeader("Upload-Offset", "0")
                    .AddTusResumableHeader()
                    .SendAsync("PATCH");

                await Task.Delay(50);

                var deleteRequest = server.CreateRequest($"/files/{fileId}")
                    .AddTusResumableHeader()
                    .SendAsync("DELETE");

                await Task.WhenAll(deleteRequest, patchRequest);

                deleteRequest.Result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
                patchRequest.Result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            }
        }
    }
}