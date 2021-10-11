using Shouldly;
using tusdotnet.Models.Concatenation;
using Xunit;

namespace tusdotnet.test.Tests.ModelTests
{
    public class UploadConcatTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Can_Parse_Upload_Concat_Header_When_Empty(string value)
        {
            var uploadConcat = new UploadConcat(value);
            uploadConcat.IsValid.ShouldBeTrue();
            uploadConcat.Type.ShouldBeNull();
        }

        [Fact]
        public void Can_Parse_Upload_Concat_Header_For_Partial()
        {
            var uploadConcat = new UploadConcat("partial");
            uploadConcat.IsValid.ShouldBeTrue();
            uploadConcat.Type.ShouldBeOfType(typeof(FileConcatPartial));

        }

        [Theory]
        [InlineData("final;/files/file1 /files/file2", 2, "file1,file2")]
        [InlineData("final;/files/file1 http://localhost/files/file2 https://example.org/files/file3?queryparam=123 https://example.org/files/file4?queryparam=123#111 https://example.org/files/file5#123", 5, "file1,file2,file3,file4,file5")]
        public void Can_Parse_Upload_Concat_Header_For_Final(string uploadConcatHeader, int expectedFileCount, string expectedFileIdCsv)
        {
            var uploadConcat = new UploadConcat(uploadConcatHeader, "/files");
            uploadConcat.IsValid.ShouldBeTrue();
            var finalConcat = uploadConcat.Type as FileConcatFinal;
            finalConcat.ShouldNotBeNull();
            finalConcat.Files.Length.ShouldBe(expectedFileCount);

            var expectedFiles = expectedFileIdCsv.Split(',');
            expectedFiles.Length.ShouldBe(expectedFileCount);

            for (int i = 0; i < finalConcat.Files.Length; i++)
            {
                finalConcat.Files[i].ShouldBe(expectedFiles[i]);
            }
        }

        [Fact]
        public void Sets_Error_If_Type_Is_Not_Final_Nor_Partial()
        {
            var uploadConcat = new UploadConcat("somevalue");
            uploadConcat.IsValid.ShouldBeFalse();
            uploadConcat.ErrorMessage.ShouldBe("Header Upload-Concat: Header is invalid. Valid values are \"partial\" and \"final\" followed by a list of file urls to concatenate");
        }

        [Theory]
        [InlineData("final")]
        [InlineData("final;")]
        [InlineData("final;  ")]
        public void Sets_Error_If_Final_Does_Not_Contain_Files(string uploadConcatHeader)
        {
            var uploadConcat = new UploadConcat(uploadConcatHeader);
            uploadConcat.IsValid.ShouldBeFalse();
            uploadConcat.ErrorMessage.ShouldBe("Header Upload-Concat: Header is invalid. Valid values are \"partial\" and \"final\" followed by a list of file urls to concatenate");
        }

        [Fact]
        public void Sets_Error_If_Final_Contains_Files_For_Other_UrlPath()
        {
            var uploadConcat = new UploadConcat("final;/otherfiles/file1 /otherfiles/file2", "/files");
            uploadConcat.IsValid.ShouldBeFalse();
            uploadConcat.ErrorMessage.ShouldBe("Header Upload-Concat: Header is invalid. Valid values are \"partial\" and \"final\" followed by a list of file urls to concatenate");
        }
    }
}
