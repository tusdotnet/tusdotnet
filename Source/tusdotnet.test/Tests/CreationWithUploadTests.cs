using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;
using tusdotnet.test.Extensions;
using Xunit;

namespace tusdotnet.test.Tests
{
    public class CreationWithUploadTests
    {
        [Fact]
        public async Task Data_Is_Written_And_201_Created_Is_Returned_If_Request_Contains_A_Body()
        {
            var fileId = Guid.NewGuid().ToString("n");
            var tusStore = Substitute.For<ITusStore, ITusCreationStore>();
            var tusCreationStore = (ITusCreationStore)tusStore;
            tusCreationStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);
            tusStore.AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(3);
            tusStore.FileExistAsync(fileId, Arg.Any<CancellationToken>()).Returns(true);
            tusStore.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(1);

            using (var server = TestServerFactory.Create(tusStore))
            {
                var response = await server
                    .CreateTusResumableRequest("/files")
                    .AddHeader("Upload-Length", "100")
                    .AddBody()
                    .SendAsync("POST");

                response.StatusCode.ShouldBe(HttpStatusCode.Created);
                response.ShouldContainHeader("Upload-Offset", "3");

                await tusStore.Received().AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
            }
        }

        [Fact]
        public async Task No_Data_Is_Written_And_201_Created_Is_Returned_If_Request_Body_Is_Empty()
        {
            var fileId = Guid.NewGuid().ToString("n");
            var tusStore = Substitute.For<ITusStore, ITusCreationStore>();
            var tusCreationStore = (ITusCreationStore)tusStore;
            tusCreationStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            using (var server = TestServerFactory.Create(tusStore))
            {
                var response = await server
                    .CreateTusResumableRequest("/files")
                    .AddHeader("Upload-Length", "100")
                    .SendAsync("POST");

                response.StatusCode.ShouldBe(HttpStatusCode.Created);
                response.ShouldNotContainHeaders("Upload-Offset");

                await tusStore.DidNotReceiveWithAnyArgs().AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
            }
        }

        [Fact]
        public async Task OnAuthorizeAsync_Is_Called_Twice_If_Request_Contains_A_Body()
        {
            var fileId = Guid.NewGuid().ToString("n");
            var tusStore = Substitute.For<ITusStore, ITusCreationStore>();
            var tusCreationStore = (ITusCreationStore)tusStore;
            tusCreationStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);
            tusStore.WithExistingFile(fileId);

            var intents = new List<IntentType>(2);
            var events = new Events
            {
                OnAuthorizeAsync = authorizeContext =>
                {
                    intents.Add(authorizeContext.Intent);
                    return Task.FromResult(0);
                }
            };

            using (var server = TestServerFactory.Create(tusStore, events))
            {
                var response = await server
                    .CreateTusResumableRequest("/files")
                    .AddHeader("Upload-Length", "100")
                    .AddBody()
                    .SendAsync("POST");

                response.StatusCode.ShouldBe(HttpStatusCode.Created);

                intents.Count.ShouldBe(2);
                intents[0].ShouldBe(IntentType.CreateFile);
                intents[1].ShouldBe(IntentType.WriteFile);
            }
        }

        [Fact]
        public async Task No_Data_Is_Written_And_201_Created_Is_Returned_With_Upload_Offset_Zero_If_OnAuthorizeAsync_Fails_For_Write_File_Intent()
        {
            var fileId = Guid.NewGuid().ToString("n");
            var tusStore = Substitute.For<ITusStore, ITusCreationStore>();
            var tusCreationStore = (ITusCreationStore)tusStore;
            tusCreationStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            var events = new Events
            {
                OnAuthorizeAsync = authorizeContext =>
                {
                    if (authorizeContext.Intent == IntentType.WriteFile)
                    {
                        authorizeContext.FailRequest(HttpStatusCode.Forbidden);
                    }

                    return Task.FromResult(0);
                }
            };

            using (var server = TestServerFactory.Create(tusStore, events))
            {
                var response = await server
                    .CreateTusResumableRequest("/files")
                    .AddHeader("Upload-Length", "100")
                    .AddBody()
                    .SendAsync("POST");

                response.StatusCode.ShouldBe(HttpStatusCode.Created);
                response.ShouldContainHeader("Upload-Offset", "0");

                await tusStore.DidNotReceiveWithAnyArgs().AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
            }
        }

        [Theory]
        [InlineData("Content-Type", "text/plain")]
        [InlineData("Upload-Checksum", "asdf1234")]
        public async Task No_Data_Is_Written_And_201_Created_Is_Returned_With_Upload_Offset_Zero_If_Write_File_Validation_Fails(string headerName, string invalidValueForHeaderName)
        {
            var fileId = Guid.NewGuid().ToString("n");
            var tusStore = Substitute.For<ITusStore, ITusCreationStore, ITusChecksumStore>();
            var tusCreationStore = (ITusCreationStore)tusStore;

            tusCreationStore.CreateFileAsync(10, null, CancellationToken.None).ReturnsForAnyArgs(fileId);
            tusStore.WithExistingFile(fileId, 10, 0);
            tusStore.AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(3);

            using (var server = TestServerFactory.Create(tusStore))
            {
                var requestBuilder = server
                    .CreateTusResumableRequest("/files")
                    .AddHeader("Upload-Length", "100")
                    .AddHeader(headerName, invalidValueForHeaderName);

                requestBuilder = headerName == "Content-Type"
                        ? requestBuilder.AddBody(invalidValueForHeaderName)
                        : requestBuilder.AddBody();

                var response = await requestBuilder.SendAsync("POST");

                response.StatusCode.ShouldBe(HttpStatusCode.Created, response.StatusCode.ToString());
                response.ShouldContainHeader("Upload-Offset", "0");

                await tusStore.DidNotReceiveWithAnyArgs().AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
            }
        }

        [Theory]
        [InlineData(typeof(Exception))]
        [InlineData(typeof(TusStoreException))]
        public async Task No_Data_Is_Written_And_201_Created_Is_Returned_With_The_Correct_Upload_Offset_If_Writing_Of_File_Fails(Type typeOfExceptionThrownByStore)
        {
            var fileId = Guid.NewGuid().ToString("n");
            var tusStore = Substitute.For<ITusStore, ITusCreationStore, ITusChecksumStore>();
            var tusCreationStore = (ITusCreationStore)tusStore;
            tusCreationStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            // Emulate that we could write 1 byte before an exception occurred.
            var exception = (Exception)Activator.CreateInstance(typeOfExceptionThrownByStore, new[] { "Test message" });
            tusStore.AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Throws(exception);
            tusStore.GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>()).Returns(1);
            tusStore.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(1);

            using (var server = TestServerFactory.Create(tusStore))
            {
                var response = await server
                    .CreateTusResumableRequest("/files")
                    .AddHeader("Upload-Length", "100")
                    .AddBody()
                    .SendAsync("POST");

                response.StatusCode.ShouldBe(HttpStatusCode.Created, response.StatusCode.ToString());
                response.ShouldContainHeader("Upload-Offset", "1");

                await tusStore.Received().AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
            }
        }

        [Fact]
        public async Task No_Data_Is_Written_And_201_Created_Is_Returned_With_Upload_Offset_Zero_If_UploadDeferLength_Is_Used_Without_ContentLength()
        {
            var fileId = Guid.NewGuid().ToString("n");
            var tusStore = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();
            var tusCreationStore = (ITusCreationStore)tusStore;
            tusCreationStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            using (var server = TestServerFactory.Create(tusStore))
            {
                var response = await server
                    .CreateTusResumableRequest("/files")
                    .AddHeader("Upload-Defer-Length", "1")
                    .AddBody()
                    .SendAsync("POST");

                response.StatusCode.ShouldBe(HttpStatusCode.Created, response.StatusCode.ToString());
                response.ShouldContainHeader("Upload-Offset", "0");

                await tusStore.DidNotReceiveWithAnyArgs().AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
            }
        }

        [Fact]
        public async Task No_Data_Is_Written_And_400_Bad_Request_Is_Returned_Without_Upload_Offset_If_UploadDeferLength_Is_Used_With_ContentLength()
        {
            var fileId = Guid.NewGuid().ToString("n");
            var tusStore = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();
            var tusCreationStore = (ITusCreationStore)tusStore;
            tusCreationStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            using (var server = TestServerFactory.Create(tusStore))
            {
                var response = await server
                    .CreateTusResumableRequest("/files")
                    .AddHeader("Upload-Defer-Length", "1")
                    .AddHeader("Upload-Length", "100")
                    .AddBody()
                    .SendAsync("POST");

                await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest, "Headers Upload-Length and Upload-Defer-Length are mutually exclusive and cannot be used in the same request");
                response.ShouldNotContainHeaders("Upload-Offset");

                await tusStore.DidNotReceiveWithAnyArgs().AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
            }
        }

        [Fact]
        public async Task Expiration_Is_Updated_After_File_Write_If_Sliding_Expiration_Is_Used()
        {
            var fileId = Guid.NewGuid().ToString("n");
            var tusStore = Substitute.For<ITusStore, ITusCreationStore, ITusExpirationStore>();
            var tusCreationStore = (ITusCreationStore)tusStore;
            var tusExpirationStore = (ITusExpirationStore)tusStore;
            tusCreationStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);
            tusStore.AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(3);
            tusStore.FileExistAsync(fileId, Arg.Any<CancellationToken>()).Returns(true);
            tusStore.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(1);

            var expiration = new SlidingExpiration(TimeSpan.FromHours(24));

            DateTimeOffset? uploadExpiresAt = null;

            using (var server = TestServerFactory.Create(app =>
             {
                 var config = new DefaultTusConfiguration
                 {
                     Store = tusStore,
                     UrlPath = "/files",
                     Expiration = expiration,
                 };

                 config.Events = new Events
                 {
                     OnAuthorizeAsync = ctx =>
                     {
                         if (ctx.Intent != IntentType.WriteFile)
                         {
                             return Task.FromResult(0);
                         }

                         // Emulate that OnCreateComplete took 10 sec to complete.
                         var fakeSystemTime = DateTimeOffset.UtcNow.AddSeconds(10);
                         config.MockSystemTime(fakeSystemTime);
                         uploadExpiresAt = fakeSystemTime.Add(expiration.Timeout);
                         return Task.FromResult(0);
                     }
                 };

                 app.UseTus(_ => config);
             }))
            {
                var response = await server
                    .CreateTusResumableRequest("/files")
                    .AddHeader("Upload-Length", "100")
                    .AddBody()
                    .SendAsync("POST");

                response.StatusCode.ShouldBe(HttpStatusCode.Created);
                // Once for file creation, once for writing the data.
                await tusExpirationStore.Received(2).SetExpirationAsync(fileId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());

                response.ShouldContainHeader("Upload-Expires", uploadExpiresAt.Value.ToString("R"));
            }
        }
    }
}
