using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.Models.Configuration;
using Xunit;

namespace tusdotnet.test.Tests.ModelTests
{
    public class EventContextTests
    {
        [Fact]
        public async Task GetFileAsync_Returns_The_File_If_FileId_Is_Set_And_Store_Supports_ITusReadableStore()
        {
            var context = new EventContextForTest
            {
                FileId = Guid.NewGuid().ToString(),
                Store = CreateReadableStoreWithExistingFile()
            };

            var file = await context.GetFileAsync();
            file.ShouldNotBeNull();
        }

        [Fact]
        public async Task GetFileAsync_Returns_Null_If_FileId_Is_Missing()
        {
            var context = new EventContextForTest
            {
                Store = CreateReadableStoreWithExistingFile()
            };

            var file = await context.GetFileAsync();
            file.ShouldBeNull();
        }

        [Fact]
        public async Task GetFileAsync_Throws_InvalidCastException_If_Store_Does_Not_Support_ITusReadableStore()
        {
            var context = new EventContextForTest
            {
                FileId = Guid.NewGuid().ToString(),
                Store = Substitute.For<ITusStore>()
            };

            await Should.ThrowAsync(async () => await context.GetFileAsync(), typeof(InvalidCastException));
        }

        private static ITusStore CreateReadableStoreWithExistingFile()
        {
            var store = Substitute.For<ITusStore, ITusReadableStore>();
            var readableStore = (ITusReadableStore)store;
            readableStore.GetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Substitute.For<ITusFile>());
            return store;
        }

        private class EventContextForTest : EventContext<EventContextForTest>
        {
        }
    }
}
