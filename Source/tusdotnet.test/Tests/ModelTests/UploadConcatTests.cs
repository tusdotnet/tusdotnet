using Shouldly;
using tusdotnet.Models.Concatenation;
using Xunit;

namespace tusdotnet.test.Tests.ModelTests
{
	public class UploadConcatTests
	{
		[Fact]
		public void Can_Parse_Upload_Concat_Header()
		{
			// None
			var uploadConcat = new UploadConcat(null);
			uploadConcat.IsValid.ShouldBeTrue();
			uploadConcat.Type.ShouldBeNull();

			uploadConcat = new UploadConcat("");
			uploadConcat.IsValid.ShouldBeTrue();
			uploadConcat.Type.ShouldBeNull();

			// Partial
			uploadConcat = new UploadConcat("partial");
			uploadConcat.IsValid.ShouldBeTrue();
			uploadConcat.Type.ShouldBeOfType(typeof(FileConcatPartial));

			// Final
			uploadConcat = new UploadConcat("final;/files/file1 /files/file2", "/files");
			uploadConcat.IsValid.ShouldBeTrue();
			var finalConcat = uploadConcat.Type as FileConcatFinal;
			finalConcat.ShouldNotBeNull();
			// ReSharper disable once PossibleNullReferenceException
			finalConcat.Files.Length.ShouldBe(2);
			finalConcat.Files[0].ShouldBe("file1");
			finalConcat.Files[1].ShouldBe("file2");
		}

		[Fact]
		public void Sets_Error_If_Type_Is_Not_Final_Nor_Partial()
		{

			var uploadConcat = new UploadConcat("somevalue");
			uploadConcat.IsValid.ShouldBeFalse();
			uploadConcat.ErrorMessage.ShouldBe("Upload-Concat header is invalid. Valid values are \"partial\" and \"final\" followed by a list of files to concatenate");
		}

		[Fact]
		public void Sets_Error_If_Final_Does_Not_Contain_Files()
		{
			var uploadConcat = new UploadConcat("final");
			uploadConcat.IsValid.ShouldBeFalse();
			uploadConcat.ErrorMessage.ShouldBe("Unable to parse Upload-Concat header");

			uploadConcat = new UploadConcat("final;");
			uploadConcat.IsValid.ShouldBeFalse();
			uploadConcat.ErrorMessage.ShouldBe("Unable to parse Upload-Concat header");

			uploadConcat = new UploadConcat("final;  ");
			uploadConcat.IsValid.ShouldBeFalse();
			uploadConcat.ErrorMessage.ShouldBe("Unable to parse Upload-Concat header");
		}

		[Fact]
		public void Sets_Error_If_Final_Contains_Files_For_Other_UrlPath()
		{
			var uploadConcat = new UploadConcat("final;/otherfiles/file1 /otherfiles/file2", "/files");
			uploadConcat.IsValid.ShouldBeFalse();
			uploadConcat.ErrorMessage.ShouldBe("Unable to parse Upload-Concat header");
		}
	}
}
