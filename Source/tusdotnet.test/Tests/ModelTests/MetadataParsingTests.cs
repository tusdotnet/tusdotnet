using System;
using System.Collections.Generic;
using System.Text;
using Shouldly;
using tusdotnet.Models;
using tusdotnet.Parsers;
using Xunit;

namespace tusdotnet.test.Tests.ModelTests
{
    public class MetadataParsingTests
    {
        private const string UploadMetadata = "filename d29ybGRfZG9taW5hdGlvbl9wbGFuLnBkZg==,othermeta c29tZSBvdGhlciBkYXRh,utf8key wrbDgMSaxafMsw==";

        [Theory]
        [MemberData(nameof(ParsingStrategies))]
        public void Parse_Can_Parse_UploadMetadata_Header(MetadataParsingStrategy? strategy)
        {
            var meta = Parse(strategy, UploadMetadata);

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

        [Theory]
        [MemberData(nameof(ParsingStrategies))]
        public void Parse_Returns_Empty_Dictionary_If_UploadMetadata_Header_Is_Null_Or_Empty(MetadataParsingStrategy? strategy)
        {
            var meta = Parse(strategy, "");
            meta.Count.ShouldBe(0);

            meta = Parse(strategy, null);
            meta.Count.ShouldBe(0);

            meta = Parse(strategy, " ");
            meta.Count.ShouldBe(0);
        }

        [Fact]
        public void GetString_Throws_Exception_If_No_Encoding_Is_Provided()
        {
            var meta = Metadata.Parse(UploadMetadata);
            meta.ContainsKey("filename").ShouldBeTrue();
            Should.Throw<ArgumentNullException>(() => meta["filename"].GetString(null));
        }

        [Theory]
        [MemberData(nameof(ParsingStrategies))]
        public void Letter_Casing_Is_Ignored_For_Metadata_Keys(MetadataParsingStrategy? strategy)
        {
            const string NoLetterCasingMetadata =
                "filename d29ybGRfZG9taW5hdGlvbl9wbGFuLnBkZg==,FILENAME c29tZSBvdGhlciBkYXRh,FiLEName wrbDgMSaxafMsw==";

            var meta = Parse(strategy, NoLetterCasingMetadata);

            meta.Count.ShouldBe(3);

            meta["filename"].GetString(Encoding.UTF8).ShouldBe("world_domination_plan.pdf");
            meta["FILENAME"].GetString(Encoding.UTF8).ShouldBe("some other data");
            meta["FiLEName"].GetString(new UTF8Encoding()).ShouldBe("¶ÀĚŧ̳");
        }

        private static Dictionary<string, Metadata> Parse(MetadataParsingStrategy? strategy, string metadataHeader)
        {
            return strategy == null
                ? Metadata.Parse(metadataHeader)
                : MetadataParser.ParseAndValidate(strategy.Value, metadataHeader).Metadata;
        }

        public static IEnumerable<object[]> ParsingStrategies => new List<object[]>(3)
        {
            new object[]{ MetadataParsingStrategy.AllowEmptyValues },
            new object[]{ MetadataParsingStrategy.Original },
            new object[] { null } // Null as a quick fix to use Metadata.Parse instead of MetadataParser.ParseAndValidate.
        };
    }
}
