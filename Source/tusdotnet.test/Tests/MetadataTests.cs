using System;
using System.Reflection;
using System.Text;
using Shouldly;
using tusdotnet.Models;
using Xunit;

namespace tusdotnet.test.Tests
{
	public class MetadataTests
	{
		private const string UploadMetadata = "filename d29ybGRfZG9taW5hdGlvbl9wbGFuLnBkZg==,othermeta c29tZSBvdGhlciBkYXRh,utf8key wrbDgMSaxafMsw==";

		[Fact]
		public void Parse_Can_Parse_UploadMetadata_Header()
		{
			var meta = Metadata.Parse(UploadMetadata);

			meta.Count.ShouldBe(3);
			meta.ContainsKey("filename").ShouldBeTrue();
			meta.ContainsKey("othermeta").ShouldBeTrue();
			meta.ContainsKey("utf8key").ShouldBeTrue();


			meta["filename"].GetBytes().ShouldBe(new byte[] { 119, 111, 114, 108, 100, 95, 100, 111, 109, 105, 110, 97, 116, 105, 111, 110, 95, 112, 108, 97, 110, 46, 112, 100, 102 });
			meta["filename"].GetString(Encoding.UTF8).ShouldBe("world_domination_plan.pdf");

			meta["othermeta"].GetBytes().ShouldBe(new byte[] { 115, 111, 109, 101, 32, 111, 116, 104, 101, 114, 32, 100, 97, 116, 97 });
			meta["othermeta"].GetString(Encoding.UTF8).ShouldBe("some other data");

			meta["utf8key"].GetBytes().ShouldBe(new byte[] { 194, 182, 195, 128, 196, 154, 197, 167, 204, 179 });
			meta["utf8key"].GetString(new UTF8Encoding()).ShouldBe("¶ÀĚŧ̳");
		}

		[Fact]
		public void Parse_Returns_Empty_Dictionary_If_UploadMetadata_Header_Is_Null_Or_Empty()
		{
			var meta = Metadata.Parse("");
			meta.Count.ShouldBe(0);

			meta = Metadata.Parse(null);
			meta.Count.ShouldBe(0);

			meta = Metadata.Parse(" ");
			meta.Count.ShouldBe(0);
		}

		[Fact]
		public void GetString_Throws_Exception_If_No_Encoding_Is_Provided()
		{
			var meta = Metadata.Parse(UploadMetadata);
			meta.ContainsKey("filename").ShouldBeTrue();
			Should.Throw<ArgumentNullException>(() => meta["filename"].GetString(null));
		}

		[Fact]
		public void Ctor_Throws_Exception_If_Encoded_Value_Is_Null_Or_Empty()
		{
			Action<ConstructorInfo, object> assertArgumentNullException = (ctor, ctorValue) =>
			{
				var exception = Should.Throw<TargetInvocationException>(() => ctor.Invoke(new[] { ctorValue }));
				exception.InnerException.ShouldNotBeNull();
				// ReSharper disable once PossibleNullReferenceException
				exception.InnerException.GetType().ShouldBe(typeof(ArgumentNullException));
			};

			// Ctor is private so use reflection to fetch it and create an instance.
			var constructor = typeof(Metadata).GetConstructor(
				BindingFlags.Instance | BindingFlags.NonPublic,
				null,
				new[] { typeof(string) },
				null
			);

			constructor.ShouldNotBeNull();

			Should.NotThrow(() => constructor.Invoke(new object[] { "d29ybGRfZG9taW5hdGlvbl9wbGFuLnBkZg==" }));

			assertArgumentNullException(constructor, null);
			assertArgumentNullException(constructor, "");
			assertArgumentNullException(constructor, " ");
		}
	}
}
