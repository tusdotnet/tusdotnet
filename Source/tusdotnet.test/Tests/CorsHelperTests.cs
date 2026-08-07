using Shouldly;
using tusdotnet.Constants;
using tusdotnet.Helpers;
using Xunit;

namespace tusdotnet.test.Tests
{
    public class CorsHelperTests
    {
        [Fact]
        public void GetExposedHeaders_Contains_Expected_Headers()
        {
            var headers = CorsHelper.GetExposedHeaders();

            headers.Length.ShouldBe(12);

            headers.ShouldContain("Location");
            headers.ShouldContain(HeaderConstants.TusResumable);
            headers.ShouldContain(HeaderConstants.TusVersion);
            headers.ShouldContain(HeaderConstants.TusExtension);
            headers.ShouldContain(HeaderConstants.TusMaxSize);
            headers.ShouldContain(HeaderConstants.TusChecksumAlgorithm);
            headers.ShouldContain(HeaderConstants.UploadLength);
            headers.ShouldContain(HeaderConstants.UploadDeferLength);
            headers.ShouldContain(HeaderConstants.UploadOffset);
            headers.ShouldContain(HeaderConstants.UploadMetadata);
            headers.ShouldContain(HeaderConstants.UploadConcat);
            headers.ShouldContain(HeaderConstants.UploadExpires);

            headers.ShouldNotContain(HeaderConstants.UploadChecksum);
        }

        [Fact]
        public void GetAllowedHeaders_Contains_Expected_Headers()
        {
            var headers = CorsHelper.GetAllowedHeaders();

            headers.Length.ShouldBe(9);

            headers.ShouldContain(HeaderConstants.TusResumable);
            headers.ShouldContain(HeaderConstants.UploadLength);
            headers.ShouldContain(HeaderConstants.UploadDeferLength);
            headers.ShouldContain(HeaderConstants.UploadOffset);
            headers.ShouldContain(HeaderConstants.UploadMetadata);
            headers.ShouldContain(HeaderConstants.UploadChecksum);
            headers.ShouldContain(HeaderConstants.UploadConcat);
            headers.ShouldContain(HeaderConstants.ContentType);
            headers.ShouldContain(HeaderConstants.XHttpMethodOveride);
        }

        [Fact]
        public void GetAllowedMethods_Contains_Expected_Methods()
        {
            var methods = CorsHelper.GetAllowedMethods();

            methods.Length.ShouldBe(5);

            methods.ShouldBe(new[] { "OPTIONS", "POST", "HEAD", "PATCH", "DELETE" });
        }
    }
}
