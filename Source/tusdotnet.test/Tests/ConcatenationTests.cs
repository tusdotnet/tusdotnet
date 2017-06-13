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
using tusdotnet.test.Data;
using tusdotnet.test.Extensions;
using Xunit;

namespace tusdotnet.test.Tests
{
    public class ConcatenationTests
    {
        [Theory, XHttpMethodOverrideData]
        public async Task Partial_Files_Can_Be_Concatenated(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusConcatenationStore>();
            var concatStore = (ITusConcatenationStore)store;
            store.FileExistAsync("partial1", Arg.Any<CancellationToken>()).Returns(true);
            store.FileExistAsync("partial2", Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync("partial1", Arg.Any<CancellationToken>()).Returns(10);
            store.GetUploadLengthAsync("partial2", Arg.Any<CancellationToken>()).Returns(20);
            store.GetUploadOffsetAsync("partial1", Arg.Any<CancellationToken>()).Returns(10);
            store.GetUploadOffsetAsync("partial2", Arg.Any<CancellationToken>()).Returns(20);
            concatStore.GetUploadConcatAsync("partial1", Arg.Any<CancellationToken>()).Returns(new FileConcatPartial());
            concatStore.GetUploadConcatAsync("partial2", Arg.Any<CancellationToken>()).Returns(new FileConcatPartial());
            concatStore.CreateFinalFileAsync(null, null, Arg.Any<CancellationToken>())
                .ReturnsForAnyArgs("finalId");

            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {
                // Create final file
                var response = await server
                    .CreateRequest("/files")
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Concat", $"final;/files/partial1 {server.BaseAddress.ToString().TrimEnd('/')}/files/partial2")
                    .OverrideHttpMethodIfNeeded("POST", methodToUse)
                    .SendAsync(methodToUse);

                response.StatusCode.ShouldBe(HttpStatusCode.Created);
                response.ShouldContainHeader("location", "/files/finalId");
                var createFinalFileCall = concatStore
                    .ReceivedCalls()
                    .Single(f => f.GetMethodInfo().Name == nameof(concatStore.CreateFinalFileAsync));
                var files = createFinalFileCall.GetArguments().First() as string[];
                // ReSharper disable once PossibleNullReferenceException
                files.Length.ShouldBe(2);
                files[0].ShouldBe("partial1");
                files[1].ShouldBe("partial2");
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_400_Bad_Request_If_Upload_Concat_Header_Is_Unparsable_Or_Not_Final_Nor_Partial(string methodToUse)
        {
            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = Substitute.For<ITusStore, ITusCreationStore, ITusConcatenationStore>(),
                    UrlPath = "/files"
                });
            }))
            {
                var invalidValues = new[]
                {
                    Guid.NewGuid().ToString(), "asdf", "123", "asdf /files1;files/2", "asdf  ", "final;file1...file2",
                    "final file2;file1;", "final  file1__file2", "final;file1 file2;file3"
                };
                foreach (var header in invalidValues)
                {
                    var response = await server
                        .CreateRequest("/files")
                        .AddTusResumableHeader()
                        .AddHeader("Upload-Concat", header)
                        .OverrideHttpMethodIfNeeded("POST", methodToUse)
                        .SendAsync(methodToUse);

                    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
                    (await response.Content.ReadAsStringAsync()).ShouldBeOneOf(
                        "Unable to parse Upload-Concat header",
                        "Upload-Concat header is invalid. Valid values are \"partial\" and \"final\" followed by a list of files to concatenate");
                }
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Head_Request_To_Final_File_Does_Not_Contain_Upload_Offset_Header_Until_Completed(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusConcatenationStore>();
            store.FileExistAsync("finalconcat", Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync("finalconcat", Arg.Any<CancellationToken>()).Returns(100);

            var offset = 10;
            // ReSharper disable once AccessToModifiedClosure
            store.GetUploadOffsetAsync("finalconcat", Arg.Any<CancellationToken>()).Returns(info => offset);

            var concatStore = (ITusConcatenationStore)store;
            concatStore.GetUploadConcatAsync("finalconcat", Arg.Any<CancellationToken>())
                .Returns(ci => new FileConcatFinal("1", "2"));

            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {
                var response = await server
                    .CreateRequest("/files/finalconcat")
                    .AddTusResumableHeader()
                    .OverrideHttpMethodIfNeeded("HEAD", methodToUse)
                    .SendAsync(methodToUse);

                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                response.ShouldContainHeader("Upload-Concat", "final;/files/1 /files/2");
                response.Headers.Contains("Upload-Offset").ShouldBeFalse();

                // Update offset and check again
                offset = 100;

                response = await server
                    .CreateRequest("/files/finalconcat")
                    .AddTusResumableHeader()
                    .OverrideHttpMethodIfNeeded("HEAD", methodToUse)
                    .SendAsync(methodToUse);

                response.ShouldContainHeader("Upload-Offset", "100");
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                response.ShouldContainHeader("Upload-Concat", "final;/files/1 /files/2");
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Head_Request_To_Partial_File_Contains_Upload_Offset_Header(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusConcatenationStore>();
            store.FileExistAsync("partialconcat", Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync("partialconcat", Arg.Any<CancellationToken>()).Returns(100);
            store.GetUploadOffsetAsync("partialconcat", Arg.Any<CancellationToken>()).Returns(10);

            var concatStore = (ITusConcatenationStore)store;
            concatStore.GetUploadConcatAsync("partialconcat", Arg.Any<CancellationToken>()).Returns(new FileConcatPartial());

            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {
                var response = await server
                    .CreateRequest("/files/partialconcat")
                    .AddTusResumableHeader()
                    .OverrideHttpMethodIfNeeded("HEAD", methodToUse)
                    .SendAsync(methodToUse);

                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                response.ShouldContainHeader("Upload-Concat", "partial");
                response.ShouldContainHeader("Upload-Offset", "10");
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Head_Request_To_Final_File_Contains_Upload_Concat_Header(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusConcatenationStore>();
            // ReSharper disable once SuspiciousTypeConversion.Global
            var concatStore = (ITusConcatenationStore)store;
            store.FileExistAsync(null, CancellationToken.None).ReturnsForAnyArgs(true);
            concatStore.GetUploadConcatAsync(null, CancellationToken.None)
                .ReturnsForAnyArgs(new FileConcatFinal("1", "2"));

            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {

                var response = await server
                    .CreateRequest("/files/concatFile")
                    .AddTusResumableHeader()
                    .SendAsync("HEAD");

                response.ShouldContainHeader("Upload-Concat", "final;/files/1 /files/2");
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Head_Request_To_Partial_File_Contains_Upload_Concat_Header(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusConcatenationStore>();
            // ReSharper disable once SuspiciousTypeConversion.Global
            var concatStore = (ITusConcatenationStore)store;
            store.FileExistAsync(null, CancellationToken.None).ReturnsForAnyArgs(true);
            concatStore.GetUploadConcatAsync(null, CancellationToken.None)
                .ReturnsForAnyArgs(new FileConcatFinal("a", "b"));

            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {

                var response = await server
                    .CreateRequest("/files/concatFile")
                    .AddTusResumableHeader()
                    .SendAsync("HEAD");

                response.ShouldContainHeader("Upload-Concat", "final;/files/a /files/b");
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Head_Request_To_Regular_File_Does_Not_Contain_Upload_Concat_Header(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusConcatenationStore>();
            // ReSharper disable once SuspiciousTypeConversion.Global
            var concatStore = (ITusConcatenationStore)store;
            store.FileExistAsync(null, CancellationToken.None).ReturnsForAnyArgs(true);
            concatStore.GetUploadConcatAsync(null, CancellationToken.None).ReturnsForAnyArgs(null as FileConcat);

            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {

                var response = await server
                    .CreateRequest("/files/concatFile")
                    .AddTusResumableHeader()
                    .SendAsync("HEAD");

                response.Headers.Contains("Upload-Concat").ShouldBeFalse();
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_403_Forbidden_When_Patching_A_Final_File(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusConcatenationStore>();
            // ReSharper disable once SuspiciousTypeConversion.Global
            var concatStore = (ITusConcatenationStore)store;
            store.FileExistAsync(null, CancellationToken.None).ReturnsForAnyArgs(true);
            concatStore.GetUploadConcatAsync(null, CancellationToken.None)
                .ReturnsForAnyArgs(new FileConcatFinal("1", "2"));

            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {
                var response = await server
                    .CreateRequest("/files/concatFileforbidden")
                    .AddTusResumableHeader()
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
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Partial_File_Metadata_Is_Not_Transfered_To_Final_File(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusConcatenationStore>();
            var concatStore = (ITusConcatenationStore)store;
            var creationStore = (ITusCreationStore)store;
            store.FileExistAsync("partial1", Arg.Any<CancellationToken>()).Returns(true);
            store.FileExistAsync("partial2", Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync("partial1", Arg.Any<CancellationToken>()).Returns(10);
            store.GetUploadLengthAsync("partial2", Arg.Any<CancellationToken>()).Returns(20);
            store.GetUploadOffsetAsync("partial1", Arg.Any<CancellationToken>()).Returns(10);
            store.GetUploadOffsetAsync("partial2", Arg.Any<CancellationToken>()).Returns(20);
            concatStore.GetUploadConcatAsync("partial1", Arg.Any<CancellationToken>()).Returns(new FileConcatPartial());
            concatStore.GetUploadConcatAsync("partial2", Arg.Any<CancellationToken>()).Returns(new FileConcatPartial());
            creationStore.GetUploadMetadataAsync("partial1", Arg.Any<CancellationToken>()).Returns("metaforpartial1");
            creationStore.GetUploadMetadataAsync("partial2", Arg.Any<CancellationToken>()).Returns("metaforpartial2");

            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {
                var response = await server
                    .CreateRequest("/files")
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Concat", "final;/files/partial1 /files/partial2")
                    .OverrideHttpMethodIfNeeded("POST", methodToUse)
                    .SendAsync(methodToUse);

                response.StatusCode.ShouldBe(HttpStatusCode.Created);
                await concatStore.Received().CreateFinalFileAsync(Arg.Any<string[]>(), null, Arg.Any<CancellationToken>());
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_400_Bad_Request_If_Any_Of_The_Partial_Files_Are_Not_Found(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusConcatenationStore>();
            // ReSharper disable once SuspiciousTypeConversion.Global
            var concatStore = (ITusConcatenationStore)store;
            concatStore.CreateFinalFileAsync(null, null, Arg.Any<CancellationToken>())
                .ThrowsForAnyArgs(info => new TusStoreException("File partial2 does not exist"));

            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {
                var response = await server
                    .CreateRequest("/files")
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Concat", "final;/files/partial1 /files/partial2")
                    .OverrideHttpMethodIfNeeded("POST", methodToUse)
                    .SendAsync(methodToUse);

                await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
                    "Could not find some of the files supplied for concatenation: partial1, partial2");

                response = await server
                    .CreateRequest("/files")
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Concat", "final;/files/partial1 /otherfiles/partial1")
                    .OverrideHttpMethodIfNeeded("POST", methodToUse)
                    .SendAsync(methodToUse);

                await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest, "Unable to parse Upload-Concat header");
            }

        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_400_Bad_Request_If_Any_Of_The_Partial_Files_Are_Not_Partial(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusConcatenationStore>();
            // ReSharper disable once SuspiciousTypeConversion.Global
            var concatStore = (ITusConcatenationStore)store;
            store.FileExistAsync("partial1", Arg.Any<CancellationToken>()).Returns(true);
            store.FileExistAsync("partial2", Arg.Any<CancellationToken>()).Returns(true);
            concatStore.GetUploadConcatAsync("partial1", Arg.Any<CancellationToken>()).Returns(new FileConcatPartial());
            concatStore.GetUploadConcatAsync("partial2", Arg.Any<CancellationToken>()).Returns(null as FileConcat);

            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {
                var response = await server
                    .CreateRequest("/files")
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Concat", "final;/files/partial1 /files/partial2")
                    .OverrideHttpMethodIfNeeded("POST", methodToUse)
                    .SendAsync(methodToUse);

                await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
                    "Some of the files supplied for concatenation are not marked as partial and can not be concatenated: partial2");
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_400_Bad_Request_If_Any_Partial_File_Is_Not_Complete(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusConcatenationStore>();
            // ReSharper disable once SuspiciousTypeConversion.Global
            var concatStore = (ITusConcatenationStore)store;
            store.FileExistAsync("partial1", Arg.Any<CancellationToken>()).Returns(true);
            store.FileExistAsync("partial2", Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync("partial1", Arg.Any<CancellationToken>()).Returns(10);
            store.GetUploadLengthAsync("partial2", Arg.Any<CancellationToken>()).Returns(20);
            store.GetUploadOffsetAsync("partial1", Arg.Any<CancellationToken>()).Returns(10);
            store.GetUploadOffsetAsync("partial2", Arg.Any<CancellationToken>()).Returns(15);
            concatStore.GetUploadConcatAsync("partial1", Arg.Any<CancellationToken>()).Returns(new FileConcatPartial());
            concatStore.GetUploadConcatAsync("partial2", Arg.Any<CancellationToken>()).Returns(new FileConcatPartial());

            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {
                var response = await server
                    .CreateRequest("/files")
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Concat", "final;/files/partial1 /files/partial2")
                    .OverrideHttpMethodIfNeeded("POST", methodToUse)
                    .SendAsync(methodToUse);

                await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
                    "Some of the files supplied for concatenation are not finished and can not be concatenated: partial2");
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Partial_File_Can_Be_Created(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusConcatenationStore>();
            var concatStore = (ITusConcatenationStore)store;
            // ReSharper disable once PossibleNullReferenceException
            concatStore.CreatePartialFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs("partial1");

            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {
                var response = await server
                    .CreateRequest("/files")
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Length", "1")
                    .AddHeader("Upload-Concat", "partial")
                    .OverrideHttpMethodIfNeeded("POST", methodToUse)
                    .SendAsync(methodToUse);

                response.StatusCode.ShouldBe(HttpStatusCode.Created);
                response.ShouldContainHeader("location", "/files/partial1");
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                // ReSharper disable once PossibleNullReferenceException
                concatStore.Received().CreatePartialFileAsync(1, null, Arg.Any<CancellationToken>());
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_413_Request_Entity_Too_Large_If_Final_File_Size_Exceeds_Tus_Max_Size(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusConcatenationStore>();
            var concatStore = (ITusConcatenationStore)store;
            store.FileExistAsync("partial1", Arg.Any<CancellationToken>()).Returns(true);
            store.FileExistAsync("partial2", Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync("partial1", Arg.Any<CancellationToken>()).Returns(10);
            store.GetUploadLengthAsync("partial2", Arg.Any<CancellationToken>()).Returns(20);
            store.GetUploadOffsetAsync("partial1", Arg.Any<CancellationToken>()).Returns(10);
            store.GetUploadOffsetAsync("partial2", Arg.Any<CancellationToken>()).Returns(20);
            concatStore.GetUploadConcatAsync("partial1", Arg.Any<CancellationToken>()).Returns(new FileConcatPartial());
            concatStore.GetUploadConcatAsync("partial2", Arg.Any<CancellationToken>()).Returns(new FileConcatPartial());
            concatStore.CreateFinalFileAsync(null, null, Arg.Any<CancellationToken>())
                .ReturnsForAnyArgs("finalId");

            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files",
                    MaxAllowedUploadSizeInBytes = 25
                });
            }))
            {
                var response = await server
                    .CreateRequest("/files")
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Concat", "final;/files/partial1 /files/partial2")
                    .OverrideHttpMethodIfNeeded("POST", methodToUse)
                    .SendAsync(methodToUse);

                await response.ShouldBeErrorResponse(HttpStatusCode.RequestEntityTooLarge,
                    "The concatenated file exceeds the server's max file size.");
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task OnUploadCompleteAsync_Is_Called_When_A_Final_File_Is_Created(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusConcatenationStore>();
            var concatStore = (ITusConcatenationStore)store;
            store.FileExistAsync("partial1", Arg.Any<CancellationToken>()).Returns(true);
            store.FileExistAsync("partial2", Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync("partial1", Arg.Any<CancellationToken>()).Returns(10);
            store.GetUploadLengthAsync("partial2", Arg.Any<CancellationToken>()).Returns(20);
            store.GetUploadOffsetAsync("partial1", Arg.Any<CancellationToken>()).Returns(10);
            store.GetUploadOffsetAsync("partial2", Arg.Any<CancellationToken>()).Returns(20);
            concatStore.GetUploadConcatAsync("partial1", Arg.Any<CancellationToken>()).Returns(new FileConcatPartial());
            concatStore.GetUploadConcatAsync("partial2", Arg.Any<CancellationToken>()).Returns(new FileConcatPartial());
            concatStore.CreateFinalFileAsync(null, null, Arg.Any<CancellationToken>())
                .ReturnsForAnyArgs("finalId");

            string callbackFileId = null;
            ITusStore callbackStore = null;
            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files",
                    OnUploadCompleteAsync = (fileId, tusStore, ct) =>
                    {
                        callbackFileId = fileId;
                        callbackStore = tusStore;
                        return Task.FromResult(0);
                    }
                });
            }))
            {
                var response = await server
                        .CreateRequest("/files")
                        .AddTusResumableHeader()
                        .AddHeader("Upload-Concat", "final;/files/partial1 /files/partial2")
                        .OverrideHttpMethodIfNeeded("POST", methodToUse)
                        .SendAsync(methodToUse);
                response.StatusCode.ShouldBe(HttpStatusCode.Created);

                callbackFileId.ShouldBe("finalId");
                callbackStore.ShouldBe(store);
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task OnUploadCompleteAsync_Is_Not_Called_When_A_Partial_File_Is_Created(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusConcatenationStore>();
            var concatStore = (ITusConcatenationStore)store;
            concatStore.CreatePartialFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs("partial1");
            concatStore.GetUploadConcatAsync("partial1", Arg.Any<CancellationToken>()).Returns(new FileConcatPartial());
            store.FileExistAsync("partial1", Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync("partial1", Arg.Any<CancellationToken>()).Returns(1);
            store.GetUploadOffsetAsync("partial1", Arg.Any<CancellationToken>()).Returns(0);
            store.AppendDataAsync("partial1", Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(1);

            var callbackCalled = false;
            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files",
                    OnUploadCompleteAsync = (fileId, tusStore, ct) =>
                    {
                        callbackCalled = true;
                        return Task.FromResult(0);
                    }
                });
            }))
            {
                var response = await server
                    .CreateRequest("/files")
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Length", "1")
                    .AddHeader("Upload-Concat", "partial")
                    .OverrideHttpMethodIfNeeded("POST", methodToUse)
                    .SendAsync(methodToUse);

                response.StatusCode.ShouldBe(HttpStatusCode.Created);
                callbackCalled.ShouldBeFalse();

                response = await server
                    .CreateRequest(response.Headers.Location.ToString())
                    .AddTusResumableHeader()
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
                callbackCalled.ShouldBeFalse();
            }
        }
    }
}