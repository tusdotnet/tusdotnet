using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Stores;
using tusdotnet.test.Helpers;
using Xunit;

namespace tusdotnet.test.Tests.ModelTests
{
    public class StoreAdapterTests
    {
        [Fact]
        public async Task Forwards_Each_Call_To_The_Store()
        {
            // Create a mock store and save stores for later use.
            var store = MockStoreHelper.CreateWithExtensions<
                ITusCreationStore,
                ITusTerminationStore,
                ITusReadableStore
            >();
            var creationStore = (ITusCreationStore)store;
            var terminationStore = (ITusTerminationStore)store;
            var readableStore = (ITusReadableStore)store;

            // Mock calls to store to be able to use Received() later.
            creationStore
                .CreateFileAsync(default, default, default)
                .ReturnsForAnyArgs(Guid.NewGuid().ToString());
            terminationStore
                .DeleteFileAsync(default, default)
                .ReturnsForAnyArgs(Task.FromResult(false));
            var tusFile = Substitute.For<ITusFile>();
            readableStore.GetFileAsync(default, default).ReturnsForAnyArgs(tusFile);

            // Create store adapter
            var storeAdapter = new StoreAdapter(store, TusExtensions.All);

            // Call each method on the store adapter with a different cancellation token to make sure that the correct one is passed to the store.
            var createFileCts = new CancellationTokenSource();
            await storeAdapter.CreateFileAsync(100, "A", createFileCts.Token);

            var deleteFileCts = new CancellationTokenSource();
            await storeAdapter.DeleteFileAsync("B", deleteFileCts.Token);

            var getFileCts = new CancellationTokenSource();
            await storeAdapter.GetFileAsync("C", getFileCts.Token);

            // Assert store got each call.
            await creationStore.Received().CreateFileAsync(100, "A", createFileCts.Token);
            await terminationStore.Received().DeleteFileAsync("B", deleteFileCts.Token);
            await readableStore.GetFileAsync("C", getFileCts.Token);
        }

        [Fact]
        public async Task Throws_InvalidOperationException_If_Store_Does_Not_Support_The_Extension()
        {
            var store = Substitute.For<ITusStore>();

            var storeAdapter = new StoreAdapter(store, TusExtensions.All);

            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await storeAdapter.CreateFileAsync(default, default, default)
            );
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await storeAdapter.VerifyChecksumAsync(default, default, default, default)
            );
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await storeAdapter.DeleteFileAsync(default, default)
            );

            store = MockStoreHelper.CreateWithExtensions<ITusTerminationStore>();
            storeAdapter = new StoreAdapter(store, TusExtensions.All);
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await storeAdapter.CreateFileAsync(default, default, default)
            );
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await storeAdapter.VerifyChecksumAsync(default, default, default, default)
            );

            try
            {
                await storeAdapter.DeleteFileAsync(default, default);
            }
            catch (InvalidOperationException)
            {
                true.ShouldBe(
                    false,
                    "Call to DeleteFileAsync caused a InvalidOperationException but should not have"
                );
            }
            catch
            {
                // ignore
            }
        }

        [Fact]
        public async Task Does_Not_Throw_InvalidOperationException_If_Store_Support_The_Extension_But_The_Extension_Has_Been_Disabled()
        {
            var store = MockStoreHelper.CreateWithExtensions<
                ITusCreationStore,
                ITusChecksumStore,
                ITusTerminationStore
            >();

            var storeAdapter = new StoreAdapter(store, TusExtensions.None);

            try
            {
                await storeAdapter.CreateFileAsync(default, default, default);
                await storeAdapter.VerifyChecksumAsync(default, default, default, default);
                await Should.ThrowAsync<InvalidOperationException>(async () =>
                    await storeAdapter.DeleteFileAsync(default, default)
                );
                await storeAdapter.DeleteFileAsync(default, default);
            }
            catch (InvalidOperationException)
            {
                true.ShouldBe(
                    false,
                    "Call to DeleteFileAsync caused a InvalidOperationException but should not have"
                );
            }
            catch
            {
                // ignore
            }
        }

        [Fact]
        public void Extension_Detection_Works_As_Expected()
        {
            var store = new TusDiskStore(System.IO.Path.GetTempPath());
            var storeAdapter = new StoreAdapter(store, TusExtensions.All);
            IEnumerable<PropertyInfo> extensionProperties = storeAdapter
                .Extensions.GetType()
                .GetProperties();

            extensionProperties = OnlyPlatformSupportedExtensions(
                storeAdapter.Extensions,
                extensionProperties
            );

            AssertAllProperties(extensionProperties, storeAdapter.Extensions, true);

            storeAdapter = new(
                store,
                TusExtensions.All.Except(
                    TusExtensions.Expiration,
                    TusExtensions.CreationWithUpload,
                    TusExtensions.ChecksumTrailer
                )
            );
            extensionProperties = storeAdapter.Extensions.GetType().GetProperties();

            var disabled = extensionProperties.Where(x =>
                x.Name
                    is nameof(TusExtensions.Expiration)
                        or nameof(TusExtensions.CreationWithUpload)
                        or nameof(TusExtensions.ChecksumTrailer)
            );
            var allOthers = extensionProperties.Except(disabled);

            AssertAllProperties(disabled, storeAdapter.Extensions, false);

            extensionProperties = OnlyPlatformSupportedExtensions(
                storeAdapter.Extensions,
                extensionProperties
            );

            AssertAllProperties(allOthers, storeAdapter.Extensions, true);

            static IEnumerable<PropertyInfo> OnlyPlatformSupportedExtensions(
                StoreExtensions storeExtensions,
                IEnumerable<PropertyInfo> extensions
            )
            {
#if !trailingheaders
                storeExtensions.ChecksumTrailer.ShouldBeFalse();
                return extensions.Where(x => x.Name is not nameof(StoreExtensions.ChecksumTrailer));
#else
                return extensions;
#endif
            }
        }

        [Fact]
        public async Task Throws_InvalidOperationException_If_Store_Does_Not_Support_Feature()
        {
            var store = Substitute.For<ITusStore>();

            var storeAdapter = new StoreAdapter(store, TusExtensions.All);

            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await storeAdapter.GetFileAsync(default, default)
            );

#if pipelines

            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await storeAdapter.AppendDataAsync(
                    default,
                    (System.IO.Pipelines.PipeReader)default,
                    default
                )
            );

#endif
        }

        [Fact]
        public void Feature_Detection_Works_As_Expected()
        {
            ITusStore store = new TusDiskStore(System.IO.Path.GetTempPath());
            var storeAdapter = new StoreAdapter(store, TusExtensions.All);

            AssertAllProperties(
                OnlyPlatformSupportedFeatures(
                    storeAdapter.Features.GetType().GetProperties(),
                    storeAdapter.Features
                ),
                storeAdapter.Features,
                true
            );

            store = Substitute.For<ITusStore>();
            storeAdapter = new(store, TusExtensions.All);

            AssertAllProperties(
                OnlyPlatformSupportedFeatures(
                    storeAdapter.Features.GetType().GetProperties(),
                    storeAdapter.Features
                ),
                storeAdapter.Features,
                false
            );

            static IEnumerable<PropertyInfo> OnlyPlatformSupportedFeatures(
                IEnumerable<PropertyInfo> features,
                StoreFeatures storeFeatures
            )
            {
#if !pipelines
                return features.Where(f => f.Name is not nameof(StoreFeatures.Pipelines));
#else
                return features;
#endif
            }
        }

        /*
         * Using ShouldAllBe fails the test:
            Message:
                Shouldly.ShouldAssertException : x
                    should satisfy the condition
                false
                    but
                [False, False]
                    do not
         */
        private static void AssertAllProperties(
            IEnumerable<PropertyInfo> properties,
            object sourceObject,
            bool expected
        )
        {
            foreach (var item in properties)
            {
                var b = (bool)item.GetValue(sourceObject);
                b.ShouldBe(expected);
            }
        }
    }
}
