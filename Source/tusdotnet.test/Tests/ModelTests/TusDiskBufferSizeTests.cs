using Shouldly;
using System;
using tusdotnet.Models;
using tusdotnet.Stores;
using Xunit;

namespace tusdotnet.test.Tests.ModelTests
{
    public class TusDiskBufferSizeTests
    {
        // Maybe: Kolla så att write buffer är större än read buffer?

        [Fact]
        public void It_Maps_Write_Buffer_Size_And_Default_Read_Buffer_Size()
        {
            var random = new Random();
            var writeBufferSize = random.Next();

            var bufferSize = new TusDiskBufferSize(writeBufferSize);

            bufferSize.WriteBufferSizeInBytes.ShouldBe(writeBufferSize);
            bufferSize.ReadBufferSizeInBytes.ShouldBe(TusDiskBufferSize.DefaultReadBufferSizeInBytes);
        }

        [Fact]
        public void It_Maps_Write_Buffer_Size_And_Read_Buffer_Size()
        {
            var random = new Random();
            var writeBufferSize = random.Next();
            var readBufferSize = random.Next();

            var bufferSize = new TusDiskBufferSize(writeBufferSize, readBufferSize);

            bufferSize.WriteBufferSizeInBytes.ShouldBe(writeBufferSize);
            bufferSize.ReadBufferSizeInBytes.ShouldBe(readBufferSize);
        }

        [Theory]
        [InlineData(1, 1, false)]
        [InlineData(1, 0, true)]
        [InlineData(0, 1, true)]
        [InlineData(-1, 1, true)]
        [InlineData(1, -1, true)]
        public void It_Throws_Exception_For_Invalid_Values(int writeBufferSize, int readBufferSize, bool shouldThrowException)
        {
            try
            {
                var bufferSize = new TusDiskBufferSize(writeBufferSize, readBufferSize);
                Assert.False(shouldThrowException);
            }
            catch(TusConfigurationException)
            {
                Assert.True(shouldThrowException);
            }
        }
    }
}
