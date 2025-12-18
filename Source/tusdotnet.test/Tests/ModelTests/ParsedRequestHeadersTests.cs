using System;
using System.Collections.Generic;
using Shouldly;
using tusdotnet.Adapters;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using Xunit;

namespace tusdotnet.test.Tests.ModelTests
{
    public class ParsedRequestHeadersTests
    {
        [Fact]
        public void Metadata_Can_Be_Set_Once_But_Throws_When_Set_Multiple_Times()
        {
            var parsedHeaders = new ParsedRequestHeaders();
            var metadata = Metadata.Parse("filename dGVzdC50eHQ=");

            parsedHeaders.Metadata = metadata;
            parsedHeaders.Metadata.ShouldBe(metadata);

            var exception = Should.Throw<InvalidOperationException>(() =>
            {
                parsedHeaders.Metadata = metadata;
            });

            exception.Message.ShouldBe("Metadata has already been set and cannot be modified.");
        }

        [Fact]
        public void UploadConcat_Can_Be_Set_Once_But_Throws_When_Set_Multiple_Times()
        {
            var parsedHeaders = new ParsedRequestHeaders();
            var uploadConcat = new UploadConcat("partial", "/files/file1");

            parsedHeaders.UploadConcat = uploadConcat;
            parsedHeaders.UploadConcat.ShouldBe(uploadConcat);

            var exception = Should.Throw<InvalidOperationException>(() =>
            {
                parsedHeaders.UploadConcat = uploadConcat;
            });

            exception.Message.ShouldBe("UploadConcat has already been set and cannot be modified.");
        }

        [Fact]
        public void UploadChecksum_Can_Be_Set_Once_But_Throws_When_Set_Multiple_Times()
        {
            var parsedHeaders = new ParsedRequestHeaders();
            var checksum = new Checksum("sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0=");

            parsedHeaders.UploadChecksum = checksum;
            parsedHeaders.UploadChecksum.ShouldBe(checksum);

            var exception = Should.Throw<InvalidOperationException>(() =>
            {
                parsedHeaders.UploadChecksum = checksum;
            });

            exception.Message.ShouldBe(
                "UploadChecksum has already been set and cannot be modified."
            );
        }

        [Fact]
        public void Metadata_Returns_Empty_Dictionary_When_Not_Set()
        {
            var parsedHeaders = new ParsedRequestHeaders();

            parsedHeaders.Metadata.ShouldNotBeNull();
            parsedHeaders.Metadata.ShouldBeEmpty();
        }

        [Fact]
        public void UploadConcat_Returns_Null_When_Not_Set()
        {
            var parsedHeaders = new ParsedRequestHeaders();

            parsedHeaders.UploadConcat.ShouldBeNull();
        }

        [Fact]
        public void UploadChecksum_Returns_Null_When_Not_Set()
        {
            var parsedHeaders = new ParsedRequestHeaders();

            parsedHeaders.UploadChecksum.ShouldBeNull();
        }
    }
}
