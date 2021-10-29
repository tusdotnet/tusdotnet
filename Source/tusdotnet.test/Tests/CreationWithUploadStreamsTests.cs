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
using tusdotnet.Models.Concatenation;
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;
using tusdotnet.test.Extensions;
using tusdotnet.test.Helpers;
using Xunit;

namespace tusdotnet.test.Tests
{
    public class CreationWithUploadStreamsTests
    {
        public static IEnumerable<object[]> UploadConcatHeadersForNonFinalFiles => new List<object[]> { new object[] { null /* not using concat at all */ }, new object[] { "partial" } };

        [Theory]
        [MemberData(nameof(UploadConcatHeadersForNonFinalFiles))]
        public async Task Data_Is_Written_And_201_Created_Is_Returned_If_Request_Contains_A_Body(string uploadConcatHeader)
        {
            var fileId = Guid.NewGuid().ToString("n");

            var tusStore = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore>();

            var tusCreationStore = (ITusCreationStore)tusStore;
            tusCreationStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            var tusConcatenationStore = (ITusConcatenationStore)tusStore;
            tusConcatenationStore.CreatePartialFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            tusStore.AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(3);
            tusStore.FileExistAsync(fileId, Arg.Any<CancellationToken>()).Returns(true);
            tusStore.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(1);

            using var server = TestServerFactory.Create(tusStore);
            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Length", "100")
                .AddHeaderIfNotEmpty("Upload-Concat", uploadConcatHeader)
                .AddBody()
                .PostAsync();

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            response.ShouldContainHeader("Upload-Offset", "3");

            await tusStore.Received().AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        }

        [Theory]
        [MemberData(nameof(UploadConcatHeadersForNonFinalFiles))]
        public async Task No_Data_Is_Written_And_201_Created_Is_Returned_If_Request_Body_Is_Empty(string uploadConcatHeader)
        {
            var fileId = Guid.NewGuid().ToString("n");
            var tusStore = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore>();

            var tusCreationStore = (ITusCreationStore)tusStore;
            tusCreationStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            var tusConcatenationStore = (ITusConcatenationStore)tusStore;
            tusConcatenationStore.CreatePartialFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            using var server = TestServerFactory.Create(tusStore);
            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Length", "100")
                .AddHeaderIfNotEmpty("Upload-Concat", uploadConcatHeader)
                .PostAsync();

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            response.ShouldNotContainHeaders("Upload-Offset");

            await tusStore.DidNotReceiveWithAnyArgs().AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        }

        [Theory]
        [MemberData(nameof(UploadConcatHeadersForNonFinalFiles))]
        public async Task OnAuthorizeAsync_Is_Called_Twice_If_Request_Contains_A_Body(string uploadConcatHeader)
        {
            var shouldUseConcatenation = !string.IsNullOrEmpty(uploadConcatHeader);

            var fileId = Guid.NewGuid().ToString("n");
            var tusStore = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore>();

            var tusCreationStore = (ITusCreationStore)tusStore;
            tusCreationStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            var tusConcatenationStore = (ITusConcatenationStore)tusStore;
            tusConcatenationStore.CreatePartialFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            tusStore.WithExistingFile(fileId);

            var intents = new List<IntentType>(2);
            var authorizeEventFileConcatenations = new List<FileConcat>(2);
            var events = new Events
            {
                OnAuthorizeAsync = authorizeContext =>
                {
                    intents.Add(authorizeContext.Intent);
                    authorizeEventFileConcatenations.Add(authorizeContext.FileConcatenation);
                    return Task.FromResult(0);
                }
            };

            using var server = TestServerFactory.Create(tusStore, events);
            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Length", "100")
                .AddHeaderIfNotEmpty("Upload-Concat", uploadConcatHeader)
                .AddBody()
                .PostAsync();

            response.StatusCode.ShouldBe(HttpStatusCode.Created);

            var authorizeIntentToCreateFile = shouldUseConcatenation ? IntentType.ConcatenateFiles : IntentType.CreateFile;

            intents.Count.ShouldBe(2);
            intents[0].ShouldBe(authorizeIntentToCreateFile);
            intents[1].ShouldBe(IntentType.WriteFile);

            authorizeEventFileConcatenations.Count.ShouldBe(2);
            authorizeEventFileConcatenations.ShouldAllBe(fc => shouldUseConcatenation ? fc is FileConcatPartial : fc == null);
        }

        [Theory]
        [MemberData(nameof(UploadConcatHeadersForNonFinalFiles))]
        public async Task No_Data_Is_Written_And_201_Created_Is_Returned_With_Upload_Offset_Zero_If_OnAuthorizeAsync_Fails_For_Write_File_Intent(string uploadConcatHeader)
        {
            var fileId = Guid.NewGuid().ToString("n");
            var tusStore = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore>();

            var tusCreationStore = (ITusCreationStore)tusStore;
            tusCreationStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            var tusConcatenationStore = (ITusConcatenationStore)tusStore;
            tusConcatenationStore.CreatePartialFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

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

            using var server = TestServerFactory.Create(tusStore, events);
            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Length", "100")
                .AddHeaderIfNotEmpty("Upload-Concat", uploadConcatHeader)
                .AddBody()
                .PostAsync();

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            response.ShouldContainHeader("Upload-Offset", "0");

            await tusStore.DidNotReceiveWithAnyArgs().AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData(null, "Content-Type", "text/plain")]
        [InlineData(null, "Upload-Checksum", "asdf1234")]
        [InlineData("partial", "Content-Type", "text/plain")]
        [InlineData("partial", "Upload-Checksum", "asdf1234")]
        public async Task No_Data_Is_Written_And_201_Created_Is_Returned_With_Upload_Offset_Zero_If_Write_File_Validation_Fails(string uploadConcatHeader, string headerName, string invalidValueForHeaderName)
        {
            var fileId = Guid.NewGuid().ToString("n");

            var tusStore = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore, ITusChecksumStore>();

            var tusCreationStore = (ITusCreationStore)tusStore;
            tusCreationStore.CreateFileAsync(10, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            var tusConcatenationStore = (ITusConcatenationStore)tusStore;
            tusConcatenationStore.CreatePartialFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            tusStore.WithExistingFile(fileId, 10, 0);
            tusStore.AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(3);

            using var server = TestServerFactory.Create(tusStore);
            var requestBuilder = server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Length", "100")
                .AddHeaderIfNotEmpty("Upload-Concat", uploadConcatHeader)
                .AddHeader(headerName, invalidValueForHeaderName);

            requestBuilder = headerName == "Content-Type"
                    ? requestBuilder.AddBody(invalidValueForHeaderName)
                    : requestBuilder.AddBody();

            var response = await requestBuilder.PostAsync();

            response.StatusCode.ShouldBe(HttpStatusCode.Created, response.StatusCode.ToString());
            response.ShouldContainHeader("Upload-Offset", "0");

            await tusStore.DidNotReceiveWithAnyArgs().AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData(null, typeof(Exception))]
        [InlineData(null, typeof(TusStoreException))]
        [InlineData("partial", typeof(Exception))]
        [InlineData("partial", typeof(TusStoreException))]
        public async Task Returns_201_Created_With_The_Correct_Upload_Offset_If_Writing_Of_File_Fails(string uploadConcatHeader, Type typeOfExceptionThrownByStore)
        {
            var fileId = Guid.NewGuid().ToString("n");
            var tusStore = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore>();

            var tusCreationStore = (ITusCreationStore)tusStore;
            tusCreationStore.CreateFileAsync(100, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            var tusConcatenationStore = (ITusConcatenationStore)tusStore;
            tusConcatenationStore.CreatePartialFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            // Emulate that we could write 1 byte before an exception occurred.
            var exception = (Exception)Activator.CreateInstance(typeOfExceptionThrownByStore, new[] { "Test message" });
            tusStore.AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Throws(exception);
            tusStore.GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>()).Returns(1);
            tusStore.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(100);

            using var server = TestServerFactory.Create(tusStore);
            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Length", "100")
                .AddHeaderIfNotEmpty("Upload-Concat", uploadConcatHeader)
                .AddBody()
                .PostAsync();

            response.StatusCode.ShouldBe(HttpStatusCode.Created, response.StatusCode.ToString());
            response.ShouldContainHeader("Upload-Offset", "1");

            await tusStore.Received().AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        }

        [Theory]
        [MemberData(nameof(UploadConcatHeadersForNonFinalFiles))]
        public async Task No_Data_Is_Written_And_201_Created_Is_Returned_With_Upload_Offset_Zero_If_UploadDeferLength_Is_Used_Without_ContentLength(string uploadConcatHeader)
        {
            var fileId = Guid.NewGuid().ToString("n");

            var tusStore = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore, ITusCreationDeferLengthStore>();

            var tusCreationStore = (ITusCreationStore)tusStore;
            tusCreationStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            var tusConcatenationStore = (ITusConcatenationStore)tusStore;
            tusConcatenationStore.CreatePartialFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            using var server = TestServerFactory.Create(tusStore);
            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Defer-Length", "1")
                .AddHeaderIfNotEmpty("Upload-Concat", uploadConcatHeader)
                .AddBody()
                .PostAsync();

            response.StatusCode.ShouldBe(HttpStatusCode.Created, response.StatusCode.ToString());
            response.ShouldContainHeader("Upload-Offset", "0");

            await tusStore.DidNotReceiveWithAnyArgs().AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        }

        [Theory]
        [MemberData(nameof(UploadConcatHeadersForNonFinalFiles))]
        public async Task No_Data_Is_Written_And_400_Bad_Request_Is_Returned_Without_Upload_Offset_If_UploadDeferLength_Is_Used_With_UploadLength(string uploadConcatHeader)
        {
            var fileId = Guid.NewGuid().ToString("n");
            var tusStore = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore, ITusCreationDeferLengthStore>();
            var tusCreationStore = (ITusCreationStore)tusStore;
            tusCreationStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            var tusConcatenationStore = (ITusConcatenationStore)tusStore;
            tusConcatenationStore.CreatePartialFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            using var server = TestServerFactory.Create(tusStore);
            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Defer-Length", "1")
                .AddHeader("Upload-Length", "100")
                .AddHeaderIfNotEmpty("Upload-Concat", uploadConcatHeader)
                .AddBody()
                .PostAsync();

            await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest, "Headers Upload-Length and Upload-Defer-Length are mutually exclusive and cannot be used in the same request");
            response.ShouldNotContainHeaders("Upload-Offset");

            await tusStore.DidNotReceiveWithAnyArgs().AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        }

        [Theory]
        [MemberData(nameof(UploadConcatHeadersForNonFinalFiles))]
        public async Task Expiration_Is_Updated_After_File_Write_If_Sliding_Expiration_Is_Used(string uploadConcatHeader)
        {
            var fileId = Guid.NewGuid().ToString("n");
            var tusStore = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusExpirationStore, ITusConcatenationStore>();

            var tusCreationStore = (ITusCreationStore)tusStore;
            tusCreationStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            var tusConcatenationStore = (ITusConcatenationStore)tusStore;
            tusConcatenationStore.CreatePartialFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            tusStore.AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(3);
            tusStore.FileExistAsync(fileId, Arg.Any<CancellationToken>()).Returns(true);
            tusStore.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(1);

            var expiration = new SlidingExpiration(TimeSpan.FromHours(24));

            DateTimeOffset? uploadExpiresAt = null;

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

                    // Emulate that OnAuthorizeAsync took 10 sec to complete.
                    var fakeSystemTime = DateTimeOffset.UtcNow.AddSeconds(10);
                    config.MockSystemTime(fakeSystemTime);
                    uploadExpiresAt = fakeSystemTime.Add(expiration.Timeout);
                    return Task.FromResult(0);
                }
            };

            using var server = TestServerFactory.Create(config);

            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Length", "100")
                .AddHeaderIfNotEmpty("Upload-Concat", uploadConcatHeader)
                .AddBody()
                .PostAsync();

            response.StatusCode.ShouldBe(HttpStatusCode.Created);

            // Once for file creation, once for writing the data.
            await ((ITusExpirationStore)tusStore).Received(2).SetExpirationAsync(fileId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());

            response.ShouldContainHeader("Upload-Expires", uploadExpiresAt.Value.ToString("R"));
        }

        [Fact]
        public async Task No_Data_Is_Written_And_201_Created_Is_Returned_With_The_Correct_Upload_Offset_If_Upload_Concat_Final_Is_Used()
        {
            var finalFileId = Guid.NewGuid().ToString();
            var partialId1 = Guid.NewGuid().ToString();
            var partialId2 = Guid.NewGuid().ToString();

            var tusStore = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore>();
            var tusConcatenationStore = (ITusConcatenationStore)tusStore;
            tusConcatenationStore.CreateFinalFileAsync(default, default, default).ReturnsForAnyArgs(finalFileId);

            tusStore
                .WithExistingPartialFile(partialId1, 100, 100)
                .WithExistingPartialFile(partialId2, 50, 50);

            using var server = TestServerFactory.Create(tusStore);
            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Concat", $"final;/files/{partialId1} /files/{partialId2}")
                .AddBody()
                .PostAsync();

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            response.Headers.Location.ToString().ShouldBe($"/files/{finalFileId}");
            response.ShouldNotContainHeaders("Upload-Offset");

            await tusStore.DidNotReceiveWithAnyArgs().AppendDataAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Runs_OnFileCompleteAsync_And_OnUploadCompleteAsync_When_Upload_Is_Complete_If_Upload_Length_Is_Zero()
        {
            // Old callback handler
            var onUploadCompleteCallCounts = 0;

            // New event handler
            var onFileCompleteAsyncCallbackCounts = 0;

            var store = Substitute.For<ITusStore, ITusCreationStore>();
            ((ITusCreationStore)store).CreateFileAsync(default, default, default).ReturnsForAnyArgs(Guid.NewGuid().ToString());
            store.GetUploadLengthAsync(default, default).ReturnsForAnyArgs(0);
            store.GetUploadOffsetAsync(default, default).ReturnsForAnyArgs(0);

            using var server = TestServerFactory.Create(new DefaultTusConfiguration
            {
                Store = store,
                UrlPath = "/files",
#pragma warning disable CS0618 // Type or member is obsolete
                OnUploadCompleteAsync = (__, ___, ____) =>
#pragma warning restore CS0618 // Type or member is obsolete
                {
                    onUploadCompleteCallCounts++;
                    return Task.FromResult(true);
                },
                Events = new Events
                {
                    OnFileCompleteAsync = __ =>
                    {
                        onFileCompleteAsyncCallbackCounts++;
                        return Task.FromResult(true);
                    }
                }
            });

            var response = await server.CreateTusResumableRequest("/files/").AddHeader("Upload-Length", "0").AddBody().SendAsync("POST");

            response.StatusCode.ShouldBe(HttpStatusCode.Created);

            onUploadCompleteCallCounts.ShouldBe(1);
            onFileCompleteAsyncCallbackCounts.ShouldBe(1);

            await store.DidNotReceiveWithAnyArgs().AppendDataAsync(default, default, default);
        }

        [Theory]
        [InlineData(1, 1, true)]
        [InlineData(2, 1, false)]
        [InlineData(500, 500, true)]
        [InlineData(500, 498, false)]
        [InlineData(10_000, 10_000, true)]
        [InlineData(10_000, 9_000, false)]
        public async Task Runs_OnFileCompleteAsync_And_OnUploadCompleteAsync_When_Upload_Is_Complete_If_Upload_Length_Is_Not_Zero_And_Entire_Body_Is_Provided(int uploadLength, int bytesInRequestBody, bool shouldRunCallbacks)
        {
            // Old callback handler
            var onUploadCompleteCallCounts = 0;

            // New event handler
            var onFileCompleteAsyncCallbackCounts = 0;

            var store = Substitute.For<ITusStore, ITusCreationStore>();
            ((ITusCreationStore)store).CreateFileAsync(default, default, default).ReturnsForAnyArgs(Guid.NewGuid().ToString());
            store.GetUploadLengthAsync(default, default).ReturnsForAnyArgs(uploadLength);
            store.GetUploadOffsetAsync(default, default).ReturnsForAnyArgs(0);
            store.AppendDataAsync(default, default, default).ReturnsForAnyArgs(bytesInRequestBody); ;

            using var server = TestServerFactory.Create(new DefaultTusConfiguration
            {
                Store = store,
                UrlPath = "/files",
#pragma warning disable CS0618 // Type or member is obsolete
                OnUploadCompleteAsync = (__, ___, ____) =>
#pragma warning restore CS0618 // Type or member is obsolete
                {
                    onUploadCompleteCallCounts++;
                    return Task.FromResult(true);
                },
                Events = new Events
                {
                    OnFileCompleteAsync = __ =>
                    {
                        onFileCompleteAsyncCallbackCounts++;
                        return Task.FromResult(true);
                    }
                }
            });

            var response = await server.CreateTusResumableRequest("/files/")
                                       .AddHeader("Upload-Length", uploadLength.ToString())
                                       .AddBody(bytesInRequestBody)
                                       .SendAsync("POST");

            response.StatusCode.ShouldBe(HttpStatusCode.Created);

            var expectedCallbackCount = shouldRunCallbacks ? 1 : 0;
            onUploadCompleteCallCounts.ShouldBe(expectedCallbackCount);
            onFileCompleteAsyncCallbackCounts.ShouldBe(expectedCallbackCount);
        }
    }
}
