using NSubstitute;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using Xunit;

namespace tusdotnet.test.Tests.ModelTests
{
    public class DefaultTusConfigurationTests
    {
        [Fact]
        public void Validate_Throws_A_TusConfigurationException_If_Store_Is_Missing()
        {
            var config = new DefaultTusConfiguration
            {
                UrlPath = "/files"
            };

            var exception = Assert.Throws<TusConfigurationException>(() => config.Validate());
            Assert.Equal("Store cannot be null.", exception.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_Throws_A_TusConfigurationException_If_UrlPath_Is_Missing(string urlPath)
        {
            var config = new DefaultTusConfiguration
            {
                Store = Substitute.For<ITusStore>(),
                UrlPath = urlPath
            };

            var exception = Assert.Throws<TusConfigurationException>(() => config.Validate());
            Assert.Equal("UrlPath cannot be empty.", exception.Message);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void Validate_Throws_A_TusConfigurationException_If_MetadataParsingStrategy_Is_Invalid(int metadataParsingStrategyAsInt)
        {
            var config = new DefaultTusConfiguration
            {
                Store = Substitute.For<ITusStore>(),
                UrlPath = "/files",
                MetadataParsingStrategy = (MetadataParsingStrategy)metadataParsingStrategyAsInt
            };

            var exception = Assert.Throws<TusConfigurationException>(() => config.Validate());
            Assert.Equal("MetadataParsingStrategy is not a valid value.", exception.Message);
        }
    }
}
