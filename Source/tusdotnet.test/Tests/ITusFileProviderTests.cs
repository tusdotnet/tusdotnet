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
        public async Task File_Id_providers_return_valid_file_ids(Type type)
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

            List<Task<string>> idTasks = new List<Task<string>>();
            for (int i = 0; i < 1_000_000; i++)
            {
                var task = provider.CreateId(string.Empty);
                idTasks.Add(task);
            }

            var ids = (await Task.WhenAll(idTasks)).ToList();

            // check for duplicates
            (ids.Count == ids.Distinct().Count()).ShouldBeTrue();
        }
    }
}
