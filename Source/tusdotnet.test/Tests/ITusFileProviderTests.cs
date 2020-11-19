using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Models.Expiration;
using tusdotnet.Stores.FileIdProviders;
using tusdotnet.test.Data;
using tusdotnet.test.Extensions;
using Xunit;

namespace tusdotnet.test.Tests
{
    public class ITusFileProviderTests
    {
        [Theory]
        [InlineData(typeof(TusGuidProvider))]
        [InlineData(typeof(TusBase64IdProvider))]
        public void File_Id_providers_return_valid_file_ids(Type type)
        {
            ITusFileIdProvider provider;
            if (type == typeof(TusGuidProvider))
            {
                provider = new TusGuidProvider();
            }
            else if (type == typeof(TusBase64IdProvider))
            {
                provider = new TusBase64IdProvider();
            }
            else throw new ArgumentException("Invalid file id provider type", nameof(type));

            var id = provider.CreateId();

            id.ShouldNotBeNullOrWhiteSpace();

            provider.ValidateId(id).ShouldBeTrue();
            provider.ValidateId("").ShouldBeFalse();
        }

        [Theory]
        [InlineData(typeof(TusGuidProvider))]
        [InlineData(typeof(TusBase64IdProvider))]
        public void File_Id_providers_return_no_duplicates(Type type)
        {
            ITusFileIdProvider provider;
            if (type == typeof(TusGuidProvider))
            {
                provider = new TusGuidProvider();
            }
            else if (type == typeof(TusBase64IdProvider))
            {
                provider = new TusBase64IdProvider();
            }
            else throw new ArgumentException("Invalid file id provider type", nameof(type));

            List<string> ids = new List<string>();
            for (int i = 0; i < 1_000_000; i++)
            {
                var id = provider.CreateId();
                id.ShouldNotBeNullOrWhiteSpace();

                ids.Add(id);
            }

            // check for duplicates
            (ids.Count == ids.Distinct().Count()).ShouldBeTrue();
        }
    }
}
