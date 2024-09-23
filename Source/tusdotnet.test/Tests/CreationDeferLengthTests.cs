using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.test.Data;
using tusdotnet.test.Extensions;
using Xunit;
using tusdotnet.test.Helpers;
using tusdotnet.Helpers;

#if netfull
using Owin;
#endif
#if netstandard
using Microsoft.AspNetCore.Builder;
#endif

namespace tusdotnet.test.Tests
{
    public class CreationDeferLengthTests
    {
        [Theory, XHttpMethodOverrideData]
        public async Task Forwards_Calls_If_The_Store_Does_Not_Support_Creation(string methodToUse)
        {
            var callForwarded = false;

            var store = Substitute.For<ITusStore, ITusCreationDeferLengthStore>();

            using var server = TestServerFactory.Create(app =>
            {
                app.UseTus(context => new DefaultTusConfiguration
                {
                    UrlPath = "/files",
                    Store = store
                });

                app.Run(ctx =>
                {
                    callForwarded = true;
                    return TaskHelper.Completed;
                });
            });

            await server.CreateRequest("/files")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .AddHeader("Upload-Defer-Length", "1")
                .SendAsync(methodToUse);

            callForwarded.ShouldBeTrue();
        }

        [Theory, XHttpMethodOverrideData]
        public async Task File_Is_Created_With_Minus_One_As_UploadLength_If_UploadDeferLength_Is_Set(string methodToUse)
        {
            var fileId = Guid.NewGuid().ToString();
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();
            var creationStore = (ITusCreationStore)store;
            creationStore.CreateFileAsync(0, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            using var server = TestServerFactory.Create(store);

            var response = await server.CreateRequest("/files")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .AddHeader("Upload-Defer-Length", "1")
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            response.ShouldContainTusResumableHeader();
            response.Headers.Location.ToString().ShouldBe($"/files/{fileId}");

            var createFileAsyncCall = creationStore.GetSingleMethodCall(nameof(creationStore.CreateFileAsync));
            createFileAsyncCall.ShouldNotBeNull();
            var uploadLength = (long)createFileAsyncCall.GetArguments()[0];
            uploadLength.ShouldBe(-1);
        }

        [Theory, XHttpMethodOverrideData]
        public async Task UploadLength_Is_Set_On_Patch_If_UploadDeferLength_Is_Set(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();
            store.FileExistAsync(null, CancellationToken.None).ReturnsForAnyArgs(true);
            store.GetUploadLengthAsync(null, CancellationToken.None).ReturnsForAnyArgs((long?)null);
            store.GetUploadOffsetAsync(null, CancellationToken.None).ReturnsForAnyArgs(0);
            store.AppendDataAsync(null, null, CancellationToken.None).ReturnsForAnyArgs(3);

            var deferLengthStore = (ITusCreationDeferLengthStore)store;
            deferLengthStore.SetUploadLengthAsync(null, 0, CancellationToken.None)
                .ReturnsForAnyArgs(Task.FromResult(true));

            using var server = TestServerFactory.Create(store);

            var response = await server.CreateRequest("/files/filedeferlength")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("PATCH", methodToUse)
                .AddHeader("Upload-Offset", "0")
                .AddHeader("Upload-Length", "3")
                .And(message => message.AddBody())
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            var setUploadLengthAsyncCall =
                deferLengthStore.GetSingleMethodCall(nameof(deferLengthStore.SetUploadLengthAsync));

            setUploadLengthAsyncCall.ShouldNotBeNull();
            var uploadLength = (long)setUploadLengthAsyncCall.GetArguments()[1];
            uploadLength.ShouldBe(3);
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_400_BadRequest_If_UploadDeferLength_Extension_Is_Disabled(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();

            using var server = TestServerFactory.Create(store, allowedExtensions: TusExtensions.All.Except(TusExtensions.CreationDeferLength));

            var response = await server.CreateTusResumableRequest("/files")
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .AddHeader("Upload-Defer-Length", "1")
                .SendAsync(methodToUse);

            await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest, "Header Upload-Defer-Length is not supported");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_400_BadRequest_If_Store_Does_Not_Support_UploadDeferLength(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore>();

            using var server = TestServerFactory.Create(store);

            var response = await server.CreateTusResumableRequest("/files")
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .AddHeader("Upload-Defer-Length", "1")
                .SendAsync(methodToUse);

            await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest, "Header Upload-Defer-Length is not supported");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_400_BadRequest_If_UploadDeferLength_Is_Not_One(string methodToUse)
        {
            var invalidDeferLengthValues = new[] { "2", Guid.NewGuid().ToString(), "hello world" };

            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();

            using var server = TestServerFactory.Create(store);

            foreach (var deferLength in invalidDeferLengthValues)
            {
                var response = await server.CreateRequest("/files")
                    .AddTusResumableHeader()
                    .OverrideHttpMethodIfNeeded("POST", methodToUse)
                    .AddHeader("Upload-Defer-Length", deferLength)
                    .SendAsync(methodToUse);

                await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
                    "Header Upload-Defer-Length must have the value '1' or be omitted");
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_400_BadRequest_If_Both_UploadLength_And_UploadDeferLength_Is_Set(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();

            using var server = TestServerFactory.Create(store);

            var response = await server.CreateRequest("/files")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .AddHeader("Upload-Length", "1")
                .AddHeader("Upload-Defer-Length", "1")
                .SendAsync(methodToUse);

            await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
                "Headers Upload-Length and Upload-Defer-Length are mutually exclusive and cannot be used in the same request");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task UploadDeferLength_Work_With_Partial_Files(string methodToUse)
        {
            var fileId = Guid.NewGuid().ToString();
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();
            var creationStore = (ITusCreationStore)store;
            creationStore.CreateFileAsync(0, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            using var server = TestServerFactory.Create(store);

            var response = await server.CreateRequest("/files")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .AddHeader("Upload-Defer-Length", "1")
                .AddHeader("Upload-Concat", "partial")
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            response.ShouldContainTusResumableHeader();
            response.Headers.Location.ToString().ShouldBe($"/files/{fileId}");

            var createFileAsyncCall = creationStore.GetSingleMethodCall(nameof(creationStore.CreateFileAsync));
            createFileAsyncCall.ShouldNotBeNull();
            var uploadLength = (long)createFileAsyncCall.GetArguments()[0];
            uploadLength.ShouldBe(-1);
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Head_Returns_UploadDeferLength_If_No_UploadLength_Has_Been_Set(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();
            store.FileExistAsync(null, CancellationToken.None).ReturnsForAnyArgs(true);
            store.GetUploadLengthAsync(null, CancellationToken.None).ReturnsForAnyArgs((long?)null);

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateRequest($"/files/{Guid.NewGuid()}")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("HEAD", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.ShouldContainHeader("Upload-Defer-Length", "1");
            response.Headers.Contains("Upload-Length").ShouldBeFalse();
        }

        [Fact]
        public async Task Multiple_Patch_Requests_Can_Be_Sent_Before_Including_UploadLength()
        {
            var fileId = Guid.NewGuid().ToString();
            // Update upload offset and upload length when set.
            var uploadOffset = 0;
            long? uploadLength = null;

            var store = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusCreationDeferLengthStore>();
            store
                .WithExistingFile(fileId, null)
                .WithAppendDataCallback(fileId, _ =>
                {
                    uploadOffset++;
                    return Task.FromResult(1L);
                }); // Each request writes one byte.

            store.GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>()).Returns(_ => uploadOffset);
            store.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(_ => uploadLength);
            ((ITusCreationDeferLengthStore)store)
                .SetUploadLengthAsync(fileId, Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(0))
                .AndDoes(ci => uploadLength = ci.Arg<long>());

            var onFileCompleteAsyncCallCount = 0;
            var events = new Events
            {
                OnFileCompleteAsync = _ =>
                {
                    onFileCompleteAsyncCallCount++;
                    return Task.FromResult(0);
                }
            };

            using var server = TestServerFactory.Create(store, events);

            for (int i = 0; i < 7; i++)
            {
                var request = server
                    .CreateTusResumableRequest($"/files/{fileId}")
                    .AddBody(1)
                    .AddHeader("Upload-Offset", uploadOffset.ToString());

                var response = await request.SendAsync("PATCH");
                response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            }

            // Event should not have been called as we do not know the completed length yet
            onFileCompleteAsyncCallCount.ShouldBe(0);

            // Include Upload-Length here to finish the upload.
            var requestWithUploadLength = server
                    .CreateTusResumableRequest($"/files/{fileId}")
                    .AddBody(1)
                    .AddHeader("Upload-Offset", uploadOffset.ToString())
                    .AddHeader("Upload-Length", "8");

            var responseWithUploadLength = await requestWithUploadLength.SendAsync("PATCH");

            responseWithUploadLength.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            onFileCompleteAsyncCallCount.ShouldBe(1);
        }

        [Fact]
        public async Task Tus_Max_Size_Is_Respected_When_Using_UploadDeferLength_And_Body_Is_A_Stream()
        {
            await Tus_Max_Size_Is_Respected_When_Using_UploadDeferLength_Internal(false);
        }

#if pipelines

        [Fact]
        public async Task Tus_Max_Size_Is_Respected_When_Using_UploadDeferLength_And_Body_Is_A_PipeReader()
        {
            await Tus_Max_Size_Is_Respected_When_Using_UploadDeferLength_Internal(true);
        }

#endif

        private async Task Tus_Max_Size_Is_Respected_When_Using_UploadDeferLength_Internal(bool usePipelinesIfAvailable)
        {
            var fileId = Guid.NewGuid().ToString();

#if pipelines
            var store = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusCreationDeferLengthStore, ITusPipelineStore>();
#else
            var store = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusCreationDeferLengthStore>();
#endif
            store.WithExistingFile(fileId, null)
                 .WithAppendDataDrainingTheRequestBody(fileId);

            var uploadOffset = 0;
            store.GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>()).Returns(_ => uploadOffset);

            var onFileCompleteAsyncCallCount = 0;
            var events = new Events
            {
                OnFileCompleteAsync = _ =>
                {
                    onFileCompleteAsyncCallCount++;
                    return Task.FromResult(0);
                }
            };

            using var server = TestServerFactory.Create(app =>
            {
                app.UseTus(_ => new()
                {
                    MaxAllowedUploadSizeInBytesLong = 5,
                    Events = events,
                    Store = store,
                    UrlPath = "/files",
#if pipelines
                    UsePipelinesIfAvailable = usePipelinesIfAvailable
#endif
                });
            });

            for (int i = 0; i < 6; i++)
            {
                var request = server
                    .CreateTusResumableRequest($"/files/{fileId}")
                    .AddBody(1)
                    .AddHeader("Upload-Offset", uploadOffset.ToString());

                var response = await request.SendAsync("PATCH");

                // Last request should fail as there is to much data.
                var expectedStatus = i == 5 ? HttpStatusCode.RequestEntityTooLarge : HttpStatusCode.NoContent;

                response.StatusCode.ShouldBe(expectedStatus);
                uploadOffset++;
            }

            // OnFileComplete should not have been called as the file contains to much data.
            onFileCompleteAsyncCallCount.ShouldBe(0);
        }

        [Fact]
        public async Task UploadLength_Is_Respected_When_Using_UploadDeferLength_And_Body_Is_A_Stream()
        {
            await UploadLength_Is_Respected_When_Using_UploadDeferLength_Internal(false);
        }

#if pipelines
        [Fact]
        public async Task UploadLength_Is_Respected_When_Using_UploadDeferLength_And_Body_Is_A_PipeReader()
        {
            await UploadLength_Is_Respected_When_Using_UploadDeferLength_Internal(true);
        }
#endif

        private static async Task UploadLength_Is_Respected_When_Using_UploadDeferLength_Internal(bool usePipelinesIfAvailable)
        {
            var fileId = Guid.NewGuid().ToString();

            long? uploadLength = null;
            var currentUploadOffset = 0;
            var currentRequestOffset = 0;

#if pipelines
            var store = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusCreationDeferLengthStore, ITusPipelineStore>();
#else
            var store = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusCreationDeferLengthStore>();
#endif
            store.WithExistingFile(
                    fileId,
                    uploadLength: _ => uploadLength,
                    uploadOffset: _ => currentUploadOffset)
                 .WithAppendDataDrainingTheRequestBody(fileId)
                 .WithSetUploadLengthCallback(fileId, size => uploadLength = size);

            var onFileCompleteAsyncCallCount = 0;
            var events = new Events
            {
                OnFileCompleteAsync = _ =>
                {
                    onFileCompleteAsyncCallCount++;
                    return Task.FromResult(0);
                }
            };

#if pipelines
            using var server = TestServerFactory.Create(store, usePipelinesIfAvailable: usePipelinesIfAvailable);
#else
            using var server = TestServerFactory.Create(store);
#endif

            for (int i = 0; i < 7; i++)
            {
                var request = server
                    .CreateTusResumableRequest($"/files/{fileId}")
                    .AddBody(2)
                    .AddHeader("Upload-Offset", currentRequestOffset.ToString());

                // Let's simulate that the client now knows the length.
                if (i == 1)
                {
                    // Set the upload length to a smaller value than the rest of the data.
                    request = request.AddHeader("Upload-Length", "5");
                }

                var response = await request.SendAsync("PATCH");

                // Request number 2 should fail due to the file being to large from the specified Upload-Length.
                if (i == 2)
                {
                    await response.ShouldBeErrorResponse(HttpStatusCode.RequestEntityTooLarge, "Request contains more data than the file's upload length");
                }
                // The rest should fail for invalid offset
                else if (i > 2)
                {
                    await response.ShouldBeErrorResponse(HttpStatusCode.Conflict, $"Offset does not match file. File offset: {currentUploadOffset}. Request offset: {currentRequestOffset}");
                }
                else
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
                    currentUploadOffset += 2;
                }

                currentRequestOffset += 2;
            }

            onFileCompleteAsyncCallCount.ShouldBe(0);
        }

        [Theory, XHttpMethodOverrideData]
        public async Task UploadLength_Must_Not_Be_Changed_Once_Set(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();
            store.FileExistAsync(null, CancellationToken.None).ReturnsForAnyArgs(true);
            store.GetUploadLengthAsync(null, CancellationToken.None).ReturnsForAnyArgs(100);

            using var server = TestServerFactory.Create(store);

            var response = await server.CreateRequest($"/files/{Guid.NewGuid()}")
                .AddTusResumableHeader()
                .AddHeader("Upload-Length", "10")
                .OverrideHttpMethodIfNeeded("PATCH", methodToUse)
                .And(m => m.AddBody())
                .SendAsync(methodToUse);

            await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
                "Upload-Length cannot be updated once set");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task OnBeforeCreateAsync_Receives_UploadLengthIsDeferred_True_If_UploadDeferLength_Has_Been_Set(
            string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();
            var creationStore = (ITusCreationStore)store;
            creationStore.CreateFileAsync(0, null, CancellationToken.None).ReturnsForAnyArgs(Guid.NewGuid().ToString());

            bool uploadIsDeferred = false;
            var events = new Events
            {
                OnBeforeCreateAsync = ctx =>
                {
                    uploadIsDeferred = ctx.UploadLengthIsDeferred;
                    return Task.FromResult(0);
                }
            };

            using var server = TestServerFactory.Create(store, events);

            var response = await server.CreateRequest("/files")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .AddHeader("Upload-Defer-Length", "1")
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            uploadIsDeferred.ShouldBeTrue();
        }

        [Theory, XHttpMethodOverrideData]
        public async Task OnCreateCompleteAsync_Receives_UploadLengthIsDeferred_True_If_UploadDeferLength_Has_Been_Set(
            string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();
            var creationStore = (ITusCreationStore)store;
            creationStore.CreateFileAsync(0, null, CancellationToken.None).ReturnsForAnyArgs(Guid.NewGuid().ToString());

            bool uploadIsDeferred = false;
            var events = new Events
            {
                OnCreateCompleteAsync = ctx =>
                {
                    uploadIsDeferred = ctx.UploadLengthIsDeferred;
                    return Task.FromResult(0);
                }
            };

            using var server = TestServerFactory.Create(store, events);

            var response = await server.CreateRequest("/files")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .AddHeader("Upload-Defer-Length", "1")
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            uploadIsDeferred.ShouldBeTrue();
        }
    }
}
