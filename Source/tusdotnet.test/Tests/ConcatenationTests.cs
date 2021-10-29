using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Models.Configuration;
using tusdotnet.test.Data;
using tusdotnet.test.Extensions;
using tusdotnet.test.Helpers;
using Xunit;

namespace tusdotnet.test.Tests
{
    public class ConcatenationTests
    {
        [Theory, XHttpMethodOverrideData]
        public async Task Partial_Files_Can_Be_Concatenated(string methodToUse)
        {
            var store = CreateStoreForFinalFileConcatenation();
            var concatStore = (ITusConcatenationStore)store;

            using var server = TestServerFactory.Create(store);

            // Create final file
            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Concat", $"final;/files/partial1 {server.BaseAddress.ToString().TrimEnd('/')}/files/partial2")
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            response.ShouldContainHeader("location", "/files/finalId");

            var createFinalFileCall = concatStore.ReceivedCalls().Single(f => f.GetMethodInfo().Name == nameof(concatStore.CreateFinalFileAsync));
            var files = (string[])createFinalFileCall.GetArguments()[0];
            files.Length.ShouldBe(2);
            files[0].ShouldBe("partial1");
            files[1].ShouldBe("partial2");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_400_Bad_Request_If_Upload_Concat_Header_Is_Unparsable_Or_Not_Final_Nor_Partial(string methodToUse)
        {
            using var server = TestServerFactory.Create(MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore>());

            var invalidValues = new[]
            {
                    Guid.NewGuid().ToString(), "asdf", "123", "asdf /files1;files/2", "asdf  ", "final;file1...file2",
                    "final file2;file1;", "final  file1__file2", "final;file1 file2;file3"
            };

            foreach (var header in invalidValues)
            {
                var response = await server
                    .CreateTusResumableRequest("/files")
                    .AddHeader("Upload-Concat", header)
                    .OverrideHttpMethodIfNeeded("POST", methodToUse)
                    .SendAsync(methodToUse);

                response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
                (await response.Content.ReadAsStringAsync()).ShouldBeOneOf(
                    "Unable to parse Upload-Concat header",
                    "Header Upload-Concat: Header is invalid. Valid values are \"partial\" and \"final\" followed by a list of file urls to concatenate");
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Head_Request_To_Final_File_Does_Not_Contain_Upload_Offset_Header_Until_Completed(string methodToUse)
        {
            var uploadOffset = 10;

            var store = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore>();
            store.FileExistAsync("finalconcat", Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync("finalconcat", Arg.Any<CancellationToken>()).Returns(100);
            store.GetUploadOffsetAsync("finalconcat", Arg.Any<CancellationToken>()).Returns(_ => uploadOffset);

            ((ITusConcatenationStore)store).GetUploadConcatAsync("finalconcat", Arg.Any<CancellationToken>()).Returns(_ => new FileConcatFinal("1", "2"));

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateTusResumableRequest("/files/finalconcat")
                .OverrideHttpMethodIfNeeded("HEAD", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.ShouldContainHeader("Upload-Concat", "final;/files/1 /files/2");
            response.Headers.Contains("Upload-Offset").ShouldBeFalse();

            // Update offset and check again
            uploadOffset = 100;

            response = await server
                .CreateTusResumableRequest("/files/finalconcat")
                .OverrideHttpMethodIfNeeded("HEAD", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.ShouldContainHeader("Upload-Offset", "100");
            response.ShouldContainHeader("Upload-Concat", "final;/files/1 /files/2");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Head_Request_To_Partial_File_Contains_Upload_Offset_Header(string methodToUse)
        {
            var store = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore>();
            store.FileExistAsync("partialconcat", Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync("partialconcat", Arg.Any<CancellationToken>()).Returns(100);
            store.GetUploadOffsetAsync("partialconcat", Arg.Any<CancellationToken>()).Returns(10);

            ((ITusConcatenationStore)store).GetUploadConcatAsync("partialconcat", Arg.Any<CancellationToken>()).Returns(new FileConcatPartial());

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateTusResumableRequest("/files/partialconcat")
                .OverrideHttpMethodIfNeeded("HEAD", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.ShouldContainHeader("Upload-Concat", "partial");
            response.ShouldContainHeader("Upload-Offset", "10");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Head_Request_To_Final_File_Contains_Upload_Concat_Header(string methodToUse)
        {
            var store = MockStoreHelper.CreateWithExtensions<ITusConcatenationStore>();
            store.FileExistAsync(null, CancellationToken.None).ReturnsForAnyArgs(true);

            ((ITusConcatenationStore)store).GetUploadConcatAsync(null, CancellationToken.None).ReturnsForAnyArgs(new FileConcatFinal("1", "2"));

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateTusResumableRequest("/files/concatFile")
                .OverrideHttpMethodIfNeeded("HEAD", methodToUse)
                .SendAsync(methodToUse);

            response.ShouldContainHeader("Upload-Concat", "final;/files/1 /files/2");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Head_Request_To_Partial_File_Contains_Upload_Concat_Header(string methodToUse)
        {
            var store = MockStoreHelper.CreateWithExtensions<ITusConcatenationStore>();
            store.FileExistAsync(null, CancellationToken.None).ReturnsForAnyArgs(true);

            ((ITusConcatenationStore)store).GetUploadConcatAsync(null, CancellationToken.None).ReturnsForAnyArgs(new FileConcatFinal("a", "b"));

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateTusResumableRequest("/files/concatFile")
                .OverrideHttpMethodIfNeeded("HEAD", methodToUse)
                .SendAsync(methodToUse);

            response.ShouldContainHeader("Upload-Concat", "final;/files/a /files/b");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Head_Request_To_Regular_File_Does_Not_Contain_Upload_Concat_Header(string methodToUse)
        {
            var store = MockStoreHelper.CreateWithExtensions<ITusConcatenationStore>();
            store.FileExistAsync(null, CancellationToken.None).ReturnsForAnyArgs(true);

            ((ITusConcatenationStore)store).GetUploadConcatAsync(null, CancellationToken.None).ReturnsForAnyArgs(null as FileConcat);

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateTusResumableRequest("/files/concatFile")
                .OverrideHttpMethodIfNeeded("HEAD", methodToUse)
                .SendAsync(methodToUse);

            response.Headers.Contains("Upload-Concat").ShouldBeFalse();
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_403_Forbidden_When_Patching_A_Final_File(string methodToUse)
        {
            var store = MockStoreHelper.CreateWithExtensions<ITusConcatenationStore>();
            store.WithExistingFile("concatFileforbidden", 10, 10);

            ((ITusConcatenationStore)store).GetUploadConcatAsync(null, CancellationToken.None).ReturnsForAnyArgs(new FileConcatFinal("1", "2"));

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateTusResumableRequest("/files/concatFileforbidden")
                .AddHeader("Upload-Offset", "0")
                .And(message =>
                {
                    message.Content = new ByteArrayContent(Encoding.ASCII.GetBytes("hello"));
                    message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
                })
                .OverrideHttpMethodIfNeeded("PATCH", methodToUse)
                .SendAsync(methodToUse);

            await response.ShouldBeErrorResponse(HttpStatusCode.Forbidden, "File with \"Upload-Concat: final\" cannot be patched");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Partial_File_Metadata_Is_Not_Transfered_To_Final_File(string methodToUse)
        {
            var store = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore>();
            var concatStore = (ITusConcatenationStore)store;
            var creationStore = (ITusCreationStore)store;

            store
                .WithExistingPartialFile("partial1", 10, 10)
                .WithExistingPartialFile("partial2", 20, 20);

            creationStore.GetUploadMetadataAsync("partial1", Arg.Any<CancellationToken>()).Returns("metaforpartial1");
            creationStore.GetUploadMetadataAsync("partial2", Arg.Any<CancellationToken>()).Returns("metaforpartial2");

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Concat", "final;/files/partial1 /files/partial2")
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            await concatStore.Received().CreateFinalFileAsync(Arg.Any<string[]>(), null, Arg.Any<CancellationToken>());
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_400_Bad_Request_If_Any_Of_The_Partial_Files_Are_Not_Found(string methodToUse)
        {
            var store = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore>();
            var concatStore = (ITusConcatenationStore)store;
            concatStore.CreateFinalFileAsync(null, null, Arg.Any<CancellationToken>())
                .ThrowsForAnyArgs(_ => new TusStoreException("File partial2 does not exist"));

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Concat", "final;/files/partial1 /files/partial2")
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .SendAsync(methodToUse);

            await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
                "Could not find some of the files supplied for concatenation: partial1, partial2");

            response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Concat", "final;/files/partial1 /otherfiles/partial1")
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .SendAsync(methodToUse);

            await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest, "Header Upload-Concat: Header is invalid. Valid values are \"partial\" and \"final\" followed by a list of file urls to concatenate");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_400_Bad_Request_If_Any_Of_The_Partial_Files_Are_Not_Partial(string methodToUse)
        {
            var store = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore>();
            var concatStore = (ITusConcatenationStore)store;
            store.FileExistAsync("partial1", Arg.Any<CancellationToken>()).Returns(true);
            store.FileExistAsync("partial2", Arg.Any<CancellationToken>()).Returns(true);
            concatStore.GetUploadConcatAsync("partial1", Arg.Any<CancellationToken>()).Returns(new FileConcatPartial());
            concatStore.GetUploadConcatAsync("partial2", Arg.Any<CancellationToken>()).Returns(null as FileConcat);

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Concat", "final;/files/partial1 /files/partial2")
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .SendAsync(methodToUse);

            await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
                "Some of the files supplied for concatenation are not marked as partial and can not be concatenated: partial2");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_400_Bad_Request_If_Any_Partial_File_Is_Not_Complete(string methodToUse)
        {
            var store = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore>();
            store
                .WithExistingPartialFile("partial1", 10, 10)
                .WithExistingPartialFile("partial2", 20, 15);

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Concat", "final;/files/partial1 /files/partial2")
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .SendAsync(methodToUse);

            await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
                "Some of the files supplied for concatenation are not finished and can not be concatenated: partial2");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Partial_File_Can_Be_Created(string methodToUse)
        {
            var store = CreateStoreForPartialFileConcatenation(returnThisPartialFileIdOnCreate: "partial1");
            var concatStore = (ITusConcatenationStore)store;

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Length", "1")
                .AddHeader("Upload-Concat", "partial")
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            response.ShouldContainHeader("location", "/files/partial1");
            await concatStore.Received().CreatePartialFileAsync(1, null, Arg.Any<CancellationToken>());
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_413_Request_Entity_Too_Large_If_Final_File_Size_Exceeds_Tus_Max_Size(string methodToUse)
        {
            var store = CreateStoreForFinalFileConcatenation();
            using var server = TestServerFactory.Create(new DefaultTusConfiguration
            {
                Store = store,
                UrlPath = "/files",
                MaxAllowedUploadSizeInBytes = 25
            });

            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Concat", "final;/files/partial1 /files/partial2")
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .SendAsync(methodToUse);

            await response.ShouldBeErrorResponse(HttpStatusCode.RequestEntityTooLarge,
                "The concatenated file exceeds the server's max file size.");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Runs_OnFileCompleteAsync_When_A_Final_File_Is_Created(string methodToUse)
        {
            var store = CreateStoreForFinalFileConcatenation();

            string oldCallbackFileId = null;
            ITusStore oldCallbackStore = null;

            string callbackFileId = null;
            ITusStore callbackStore = null;

            using var server = TestServerFactory.Create(new DefaultTusConfiguration
            {
                Store = store,
                UrlPath = "/files",
#pragma warning disable CS0618 // Type or member is obsolete
                OnUploadCompleteAsync = (fileId, tusStore, ct) =>
#pragma warning restore CS0618 // Type or member is obsolete
                {
                    oldCallbackFileId = fileId;
                    oldCallbackStore = tusStore;
                    return Task.FromResult(0);
                },
                Events = new Events
                {
                    OnFileCompleteAsync = ctx =>
                    {
                        callbackFileId = ctx.FileId;
                        callbackStore = ctx.Store;
                        return Task.FromResult(0);
                    }
                }
            });

            var response = await server
                    .CreateTusResumableRequest("/files")
                    .AddHeader("Upload-Concat", "final;/files/partial1 /files/partial2")
                    .OverrideHttpMethodIfNeeded("POST", methodToUse)
                    .SendAsync(methodToUse);
            response.StatusCode.ShouldBe(HttpStatusCode.Created);

            oldCallbackFileId.ShouldBe("finalId");
            oldCallbackStore.ShouldBe(store);

            callbackFileId.ShouldBe("finalId");
            callbackStore.ShouldBe(store);
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Does_Not_Run_OnFileCompleteAsync_When_A_Partial_File_Is_Created(string methodToUse)
        {
            var store = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore>();
            store.WithExistingPartialFile("partial1");
            store.AppendDataAsync("partial1", Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(1);

            var concatStore = (ITusConcatenationStore)store;
            concatStore.CreatePartialFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs("partial1");
            concatStore.GetUploadConcatAsync("partial1", Arg.Any<CancellationToken>()).Returns(new FileConcatPartial());

            var oldCallbackCalled = false;
            var callbackCalled = false;

            using var server = TestServerFactory.Create(new DefaultTusConfiguration
            {
                Store = store,
                UrlPath = "/files",
#pragma warning disable CS0618 // Type or member is obsolete
                OnUploadCompleteAsync = (fileId, tusStore, ct) =>
#pragma warning restore CS0618 // Type or member is obsolete
                {
                    oldCallbackCalled = true;
                    return Task.FromResult(0);
                },
                Events = new Events
                {
                    OnFileCompleteAsync = ctx =>
                    {
                        callbackCalled = true;
                        return Task.FromResult(0);
                    }
                }
            });

            // Test that it does not run when creating the partial file.
            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Length", "1")
                .AddHeader("Upload-Concat", "partial")
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            oldCallbackCalled.ShouldBeFalse();
            callbackCalled.ShouldBeFalse();

            // Test that it does not run when the data transfer to the partial file is complete.
            response = await server
                .CreateTusResumableRequest(response.Headers.Location.ToString())
                .AddHeader("Upload-Offset", "0")
                .OverrideHttpMethodIfNeeded("PATCH", methodToUse)
                .And(m =>
                {
                    var content = new ByteArrayContent(new byte[] { 1 });
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
                    m.Content = content;
                })
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            oldCallbackCalled.ShouldBeFalse();
            callbackCalled.ShouldBeFalse();
        }

        [Theory, XHttpMethodOverrideData]
        public async Task OnBeforeCreateAsync_Receives_FileConcatPartial_For_Partial_Files(string methodToUse)
        {
            FileConcat fileConcat = null;

            using var server = TestServerFactory.Create(CreateStoreForPartialFileConcatenation(), new Events
            {
                OnBeforeCreateAsync = ctx =>
                {
                    fileConcat = ctx.FileConcatenation;
                    return Task.FromResult(0);
                }
            });

            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Length", "1")
                .AddHeader("Upload-Concat", "partial")
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            fileConcat.ShouldBeOfType<FileConcatPartial>();
        }

        [Theory, XHttpMethodOverrideData]
        public async Task OnBeforeCreateAsync_Receives_FileConcatFinal_For_Final_Files(string methodToUse)
        {
            FileConcat fileConcat = null;

            using var server = TestServerFactory.Create(CreateStoreForFinalFileConcatenation(), new Events
            {
                OnBeforeCreateAsync = ctx =>
                {
                    fileConcat = ctx.FileConcatenation;
                    return Task.FromResult(0);
                }
            });

            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Concat", "final;/files/partial1 /files/partial2")
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            fileConcat.ShouldBeOfType<FileConcatFinal>();
            var fileConcatFinal = (FileConcatFinal)fileConcat;
            fileConcatFinal.Files.Length.ShouldBe(2);
            fileConcatFinal.Files.All(f => f == "partial1" || f == "partial2").ShouldBeTrue();
        }

        [Theory, XHttpMethodOverrideData]
        public async Task OnCreateCompleteAsync_Receives_FileConcatPartial_For_Partial_Files(string methodToUse)
        {
            FileConcat fileConcat = null;

            using var server = TestServerFactory.Create(CreateStoreForPartialFileConcatenation(), new Events
            {
                OnCreateCompleteAsync = ctx =>
                {
                    fileConcat = ctx.FileConcatenation;
                    return Task.FromResult(0);
                }
            });

            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Length", "1")
                .AddHeader("Upload-Concat", "partial")
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            fileConcat.ShouldBeOfType<FileConcatPartial>();
        }

        [Theory, XHttpMethodOverrideData]
        public async Task OnCreateCompleteAsync_Receives_FileConcatFinal_For_Final_Files(string methodToUse)
        {
            FileConcat fileConcat = null;

            using var server = TestServerFactory.Create(CreateStoreForFinalFileConcatenation(), new Events
            {
                OnCreateCompleteAsync = ctx =>
                {
                    fileConcat = ctx.FileConcatenation;
                    return Task.FromResult(0);
                }
            });

            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Concat", "final;/files/partial1 /files/partial2")
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            var fileConcatFinal = fileConcat.ShouldBeOfType<FileConcatFinal>();
            fileConcatFinal.Files.Length.ShouldBe(2);
            fileConcatFinal.Files.All(f => f == "partial1" || f == "partial2").ShouldBeTrue();
        }

        [Fact]
        public async Task OnAuthorized_Is_Called_For_Partial_Files()
        {
            var onAuthorizeWasCalled = false;
            IntentType? intentProvidedToOnAuthorize = null;
            FileConcat fileConcatProvidedToOnAuthorize = null;

            using var server = TestServerFactory.Create(CreateStoreForPartialFileConcatenation("partial1"), new Events
            {
                OnAuthorizeAsync = ctx =>
                {
                    onAuthorizeWasCalled = true;
                    intentProvidedToOnAuthorize = ctx.Intent;
                    fileConcatProvidedToOnAuthorize = ctx.FileConcatenation;
                    return Task.FromResult(0);
                }
            });

            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Length", "1")
                .AddHeader("Upload-Concat", "partial")
                .SendAsync("POST");

            response.ShouldContainTusResumableHeader();
            response.ShouldContainHeader("location", "/files/partial1");

            onAuthorizeWasCalled.ShouldBeTrue();
            intentProvidedToOnAuthorize.ShouldBe(IntentType.ConcatenateFiles);
            fileConcatProvidedToOnAuthorize.ShouldBeOfType<FileConcatPartial>();
        }

        [Fact]
        public async Task OnAuthorized_Is_Called_For_Final_Files()
        {
            var onAuthorizeWasCalled = false;
            IntentType? intentProvidedToOnAuthorize = null;
            FileConcat fileConcatProvidedToOnAuthorize = null;

            using var server = TestServerFactory.Create(CreateStoreForFinalFileConcatenation(), new Events
            {
                OnAuthorizeAsync = ctx =>
                {
                    onAuthorizeWasCalled = true;
                    intentProvidedToOnAuthorize = ctx.Intent;
                    fileConcatProvidedToOnAuthorize = ctx.FileConcatenation;
                    return Task.FromResult(0);
                }
            });

            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Concat", "final;/files/partial1 /files/partial2")
                .SendAsync("POST");

            response.ShouldContainTusResumableHeader();
            response.ShouldContainHeader("location", "/files/finalId");

            onAuthorizeWasCalled.ShouldBeTrue();
            intentProvidedToOnAuthorize.ShouldBe(IntentType.ConcatenateFiles);
            var fileConcatFinal = fileConcatProvidedToOnAuthorize.ShouldBeOfType<FileConcatFinal>();
            fileConcatFinal.Files.Length.ShouldBe(2);
            fileConcatFinal.Files.All(f => f == "partial1" || f == "partial2").ShouldBeTrue();
        }

        [Fact]
        public async Task Request_Is_Cancelled_If_OnAuthorized_Fails_The_Request_For_Partial_Files()
        {
            using var server = TestServerFactory.Create(CreateStoreForPartialFileConcatenation(), new Events
            {
                OnAuthorizeAsync = ctx =>
                {
                    ctx.FailRequest(HttpStatusCode.Unauthorized);
                    return Task.FromResult(0);
                }
            });

            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Length", "1")
                .AddHeader("Upload-Concat", "partial")
                .SendAsync("POST");

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            response.ShouldNotContainHeaders("Tus-Resumable", "Location", "Content-Type");
        }

        [Fact]
        public async Task Request_Is_Cancelled_If_OnAuthorized_Fails_The_Request_For_Final_Files()
        {
            using var server = TestServerFactory.Create(CreateStoreForFinalFileConcatenation(), new Events
            {
                OnAuthorizeAsync = ctx =>
                {
                    ctx.FailRequest(HttpStatusCode.Unauthorized);
                    return Task.FromResult(0);
                }
            });

            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Concat", "final;/files/partial1 /files/partial2")
                .SendAsync("POST");

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            response.ShouldNotContainHeaders("Tus-Resumable", "Location", "Content-Type");
        }

        private static ITusStore CreateStoreForFinalFileConcatenation()
        {
            var store = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore>();
            store
                .WithExistingPartialFile("partial1", 10, 10)
                .WithExistingPartialFile("partial2", 20, 20);

            var concatStore = (ITusConcatenationStore)store;
            concatStore.CreateFinalFileAsync(null, null, Arg.Any<CancellationToken>()).ReturnsForAnyArgs("finalId");

            return store;
        }

        private static ITusStore CreateStoreForPartialFileConcatenation(string returnThisPartialFileIdOnCreate = null)
        {
            var store = MockStoreHelper.CreateWithExtensions<ITusCreationStore, ITusConcatenationStore>();

            var concatStore = (ITusConcatenationStore)store;
            concatStore.CreatePartialFileAsync(-1, null, CancellationToken.None).ReturnsForAnyArgs(returnThisPartialFileIdOnCreate ?? Guid.NewGuid().ToString());

            return store;
        }
    }
}