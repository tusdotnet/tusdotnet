using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Models.Concatenation;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class UploadConcatForConcatenateFiles : Requirement
    {
        private readonly UploadConcat _uploadConcat;

        public UploadConcatForConcatenateFiles(UploadConcat uploadConcat)
        {
            _uploadConcat = uploadConcat;
        }

        public override async Task Validate(ContextAdapter context)
        {
            if (!_uploadConcat.IsValid)
            {
                await BadRequest(_uploadConcat.ErrorMessage);
                return;
            }

            if (_uploadConcat.Type is FileConcatFinal finalConcat)
            {
                await ValidateFinalFileCreation(finalConcat, context);
            }
        }

        private async Task ValidateFinalFileCreation(
            FileConcatFinal finalConcat,
            ContextAdapter context
        )
        {
            var filesExist = await Task.WhenAll(
                finalConcat.Files.Select(f =>
                    context.StoreAdapter.FileExistAsync(f, context.CancellationToken)
                )
            );

            if (filesExist.Any(f => !f))
            {
                var missing = new List<string>(filesExist.Length);
                for (var i = 0; i < filesExist.Length; i++)
                {
                    if (!filesExist[i])
                        missing.Add(finalConcat.Files[i]);
                }
                await BadRequest(
                    $"Could not find some of the files supplied for concatenation: {string.Join(", ", missing)}"
                );
                return;
            }

            var filesArePartial = await Task.WhenAll(
                finalConcat.Files.Select(f =>
                    context.StoreAdapter.GetUploadConcatAsync(f, context.CancellationToken)
                )
            );

            if (filesArePartial.Any(f => f is not FileConcatPartial))
            {
                var notPartial = new List<string>(filesArePartial.Length);
                for (var i = 0; i < filesArePartial.Length; i++)
                {
                    if (filesArePartial[i] is not FileConcatPartial)
                        notPartial.Add(finalConcat.Files[i]);
                }
                await BadRequest(
                    $"Some of the files supplied for concatenation are not marked as partial and can not be concatenated: {string.Join(", ", notPartial)}"
                );
                return;
            }

            var incompleteFiles = new List<string>(finalConcat.Files.Length);
            var totalSize = 0L;
            foreach (var file in finalConcat.Files)
            {
                var length = context.StoreAdapter.GetUploadLengthAsync(
                    file,
                    context.CancellationToken
                );
                var offset = context.StoreAdapter.GetUploadOffsetAsync(
                    file,
                    context.CancellationToken
                );
                await Task.WhenAll(length, offset);

                if (length.Result != null)
                {
                    totalSize += length.Result.Value;
                }

                if (length.Result != offset.Result)
                {
                    incompleteFiles.Add(file);
                }
            }

            if (incompleteFiles.Count > 0)
            {
                await BadRequest(
                    $"Some of the files supplied for concatenation are not finished and can not be concatenated: {string.Join(", ", incompleteFiles)}"
                );
                return;
            }

            if (totalSize > context.Configuration.GetMaxAllowedUploadSizeInBytes())
            {
                await RequestEntityTooLarge(
                    "The concatenated file exceeds the server's max file size."
                );
            }
        }
    }
}
