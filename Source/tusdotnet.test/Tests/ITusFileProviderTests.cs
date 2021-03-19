using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.Stores.FileIdProviders;
using Xunit;

namespace tusdotnet.test.Tests
{
    public class ITusFileProviderTests
    {
        [Theory]
        [InlineData(typeof(TusGuidProvider))]
        [InlineData(typeof(TusBase64IdProvider))]
        public async Task File_Id_providers_return_valid_file_ids(Type type)
        {
            var provider = GetProvider(type);

            var id = await provider.CreateId(string.Empty);

            id.ShouldNotBeNullOrWhiteSpace();

            (await provider.ValidateId(id)).ShouldBeTrue();
            (await provider.ValidateId("")).ShouldBeFalse();
        }

        [Theory]
        [InlineData(typeof(TusGuidProvider))]
        [InlineData(typeof(TusBase64IdProvider))]
        public async Task File_Id_providers_return_no_duplicates(Type type)
        {
            var provider = GetProvider(type);

            var idTasks = new List<Task<string>>();
            for (int i = 0; i < 1_000_000; i++)
            {
                var task = provider.CreateId(string.Empty);
                idTasks.Add(task);
            }

            var ids = (await Task.WhenAll(idTasks)).ToList();

            // check for duplicates
            (ids.Count == ids.Distinct().Count()).ShouldBeTrue();
        }

        private ITusFileIdProvider GetProvider(Type type)
        {
            if (type == typeof(TusGuidProvider))
            {
                return new TusGuidProvider();
            }

            if (type == typeof(TusBase64IdProvider))
            {
                return new TusBase64IdProvider();
            }

            throw new ArgumentException("Invalid file id provider type", nameof(type));
        }
    }
}
