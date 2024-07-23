using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Interfaces;
using tusdotnet.Models.Concatenation;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class UploadConcatForConcatenateFiles : Requirement
    {
        private readonly Models.Concatenation.UploadConcat _uploadConcat;
        private readonly ITusConcatenationStore _concatenationStore;

        public UploadConcatForConcatenateFiles(Models.Concatenation.UploadConcat uploadConcat, ITusConcatenationStore concatenationStore)
        {
            _uploadConcat = uploadConcat;
            _concatenationStore = concatenationStore;
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

        private async Task ValidateFinalFileCreation(FileConcatFinal finalConcat, ContextAdapter context)
        {
            var filesExist = await Task.WhenAll(finalConcat.Files.Select(f =>
                context.Configuration.Store.FileExistAsync(f, context.CancellationToken)));

            if (filesExist.Any(f => !f))
            {
                await BadRequest(
                    $"Could not find some of the files supplied for concatenation: {string.Join(", ", filesExist.Zip(finalConcat.Files, (b, s) => new { exist = b, name = s }).Where(f => !f.exist).Select(f => f.name))}");
                return;
            }

            var filesArePartial = await Task.WhenAll(
                finalConcat.Files.Select(f => _concatenationStore.GetUploadConcatAsync(f, context.CancellationToken)));

            if (filesArePartial.Any(f => !(f is FileConcatPartial)))
            {
                await BadRequest($"Some of the files supplied for concatenation are not marked as partial and can not be concatenated: {string.Join(", ", filesArePartial.Zip(finalConcat.Files, (s, s1) => new { partial = s is FileConcatPartial, name = s1 }).Where(f => !f.partial).Select(f => f.name))}");
                return;
            }

            var incompleteFiles = new List<string>(finalConcat.Files.Length);
            var totalSize = 0L;
            foreach (var file in finalConcat.Files)
            {
                var length = context.Configuration.Store.GetUploadLengthAsync(file, context.CancellationToken);
                var offset = context.Configuration.Store.GetUploadOffsetAsync(file, context.CancellationToken);
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
                    $"Some of the files supplied for concatenation are not finished and can not be concatenated: {string.Join(", ", incompleteFiles)}");
                return;
            }

            if (totalSize > context.Configuration.GetMaxAllowedUploadSizeInBytes())
            {
                await RequestEntityTooLarge("The concatenated file exceeds the server's max file size.");
            }
        }
    }
}
