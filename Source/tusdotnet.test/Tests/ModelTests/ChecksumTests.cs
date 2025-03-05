using Shouldly;
using tusdotnet.Models;
using Xunit;

namespace tusdotnet.test.Tests.ModelTests
{
    public class ChecksumTests
    {
        [Fact]
        public void Can_Parse_Upload_Checksum_Header()
        {
            var checksum = new Checksum("sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0=");
            checksum.IsValid.ShouldBeTrue();
            checksum.Algorithm.ShouldBe("sha1");
            checksum.Hash.ShouldNotBeNull();
        }

        [Fact]
        public void Sets_Error_If_Header_Does_Not_Contain_Algorithm_And_Hash()
        {
            var checksum = new Checksum("sha1");
            checksum.IsValid.ShouldBeFalse();
            checksum.Algorithm.ShouldBeNull();
            checksum.Hash.ShouldBeNull();

            checksum = new Checksum("");
            checksum.IsValid.ShouldBeFalse();
            checksum.Algorithm.ShouldBeNull();
            checksum.Hash.ShouldBeNull();

            checksum = new Checksum(" ");
            checksum.IsValid.ShouldBeFalse();
            checksum.Algorithm.ShouldBeNull();
            checksum.Hash.ShouldBeNull();
        }

        [Fact]
        public void Sets_Error_If_Algoithm_Is_Empty()
        {
            var checksum = new Checksum(" Kq5sNclPz7QV2+lfQIuc6R7oRu0=");
            checksum.IsValid.ShouldBeFalse();
            checksum.Algorithm.ShouldBeNull();
            checksum.Hash.ShouldBeNull();
        }

        [Fact]
        public void Sets_Error_If_Hash_Is_Empty()
        {
            var checksum = new Checksum("sha1 ");
            checksum.IsValid.ShouldBeFalse();
            checksum.Algorithm.ShouldBeNull();
            checksum.Hash.ShouldBeNull();

            checksum = new Checksum("sha1    ");
            checksum.IsValid.ShouldBeFalse();
            checksum.Algorithm.ShouldBeNull();
            checksum.Hash.ShouldBeNull();
        }

        [Fact]
        public void Sets_Error_If_Hash_Is_Not_Base64_Encoded()
        {
            var checksum = new Checksum("sha1 Kq5sNclPz7QV2+=");
            checksum.IsValid.ShouldBeFalse();
            checksum.Algorithm.ShouldBeNull();
            checksum.Hash.ShouldBeNull();
        }
    }
}
