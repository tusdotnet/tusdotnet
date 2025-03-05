using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;
using Xunit;

namespace tusdotnet.test.Tests.ExtensionTests
{
    public class ITusFileExtensionsTests
    {
        [Theory]
        [InlineData(0, 100, false)]
        [InlineData(25, 50, false)]
        [InlineData(100, 100, true)]
        public async Task Returns_Correct_File_Upload_Status(
            int offset,
            int length,
            bool expectedCompleted
        )
        {
            var file = Substitute.For<ITusFile>();
            var store = Substitute.For<ITusStore>();
            store
                .GetUploadLengthAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(length);
            store
                .GetUploadOffsetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(offset);

            var actualComplete = await file.IsCompleteAsync(store, CancellationToken.None);
            actualComplete.ShouldBe(expectedCompleted);
        }
    }
}
