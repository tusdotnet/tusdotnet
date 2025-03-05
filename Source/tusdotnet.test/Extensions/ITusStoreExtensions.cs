using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.Core;
using tusdotnet.Interfaces;

namespace tusdotnet.test.Extensions
{
    internal static class ITusStoreExtensions
    {
        internal static ITusStore WithExistingPartialFile(
            this ITusStore store,
            string fileId,
            long? uploadLength = 1,
            long uploadOffset = 0
        )
        {
            store.WithExistingFile(fileId, uploadLength, uploadOffset);

            ((ITusConcatenationStore)store)
                .GetUploadConcatAsync(fileId, Arg.Any<CancellationToken>())
                .Returns(new Models.Concatenation.FileConcatPartial());

            return store;
        }

        internal static ITusStore WithExistingFile(
            this ITusStore store,
            string fileId,
            long? uploadLength = 1,
            long uploadOffset = 0
        )
        {
            return store.WithExistingFile(fileId, _ => uploadLength, _ => uploadOffset);
        }

        internal static ITusStore WithExistingFile(
            this ITusStore store,
            string fileId,
            Func<CallInfo, long?> uploadLength,
            Func<CallInfo, long> uploadOffset
        )
        {
            store.FileExistAsync(fileId, Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(uploadLength);
            store.GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>()).Returns(uploadOffset);
            return store;
        }

        internal static ITusStore WithAppendDataDrainingTheRequestBody(
            this ITusStore store,
            string fileId
        )
        {
            return store.WithAppendDataCallback(
                fileId,
                async ci =>
                {
                    var stream = ci.Args().FirstOrDefault(f => f is Stream);
                    if (stream is Stream s)
                    {
                        var read = 0;
                        var totalBodySize = 0;
                        do
                        {
                            read = await s.ReadAsync(new byte[100], 0, 100);
                            totalBodySize += read;
                        } while (read != 0);

                        return totalBodySize;
                    }
#if pipelines

                    var pipeReader = ci.Args().First(f => f is System.IO.Pipelines.PipeReader);
                    if (pipeReader is System.IO.Pipelines.PipeReader p)
                    {
                        await p.ReadAsync(default);
                    }

#endif
                    return 1;
                }
            );
        }

        internal static ITusStore WithAppendDataCallback(
            this ITusStore store,
            string fileId,
            Func<CallInfo, Task<long>> onInvokeAppendData
        )
        {
            store
                .AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(onInvokeAppendData);

#if pipelines

            if (store is ITusPipelineStore pipelineStore)
            {
                pipelineStore
                    .AppendDataAsync(
                        fileId,
                        Arg.Any<System.IO.Pipelines.PipeReader>(),
                        Arg.Any<CancellationToken>()
                    )
                    .Returns(onInvokeAppendData);
            }
#endif

            return store;
        }

        internal static ITusStore WithSetUploadLengthCallback(
            this ITusStore store,
            string fileId,
            Action<long> callback
        )
        {
            if (store is not ITusCreationDeferLengthStore creationDeferLengthStore)
                throw new ArgumentException("Store is not a ITusCreationDeferLengthStore");

            creationDeferLengthStore
                .SetUploadLengthAsync(fileId, Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var size = ci.Arg<long>();
                    callback(size);
                    return Task.FromResult(0);
                });

            return store;
        }
    }
}
