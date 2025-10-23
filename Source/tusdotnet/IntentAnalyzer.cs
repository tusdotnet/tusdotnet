#nullable enable
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.IntentHandlers;
using tusdotnet.Models.Concatenation;

namespace tusdotnet
{
    internal static class IntentAnalyzer
    {
        internal static async Task<MultiIntentHandler?> DetermineIntent(ContextAdapter context)
        {
            var firstIntent = DetermineIntentFromRequest(context);

            if (firstIntent == IntentHandler.NotApplicable)
                return null;

            // Detect intents for "creation with upload"
            var secondIntent = await IntentIncludesCreationWithUpload(firstIntent, context);

            if (secondIntent is null)
                return new(context, firstIntent);

            return new(context, firstIntent, secondIntent);
        }

        private static async Task<IntentHandler?> IntentIncludesCreationWithUpload(
            IntentHandler firstIntent,
            ContextAdapter context
        )
        {
            if (firstIntent is not CreateFileHandler and not ConcatenateFilesHandler)
                return null;

            // Final files does not support writing, i.e. we should not do anything as the second intent.
            if (
                firstIntent is ConcatenateFilesHandler concatenateFilesHandler
                && concatenateFilesHandler.UploadConcat.Type is FileConcatFinal
            )
                return null;

            try
            {
                var writeFileContext =
                    await WriteFileContextForCreationWithUpload.FromCreationContext(context);

                if (!writeFileContext.FileContentIsAvailable)
                    return null;

                // Only need to replace the body as the body reader already supports buffering.
                context.Request.Body = writeFileContext.Body;

                return new WriteFileHandler(context, true);
            }
            catch
            {
                return null;
            }
        }

        private static IntentHandler DetermineIntentFromRequest(ContextAdapter context)
        {
            var httpMethod = GetHttpMethod(context.Request);

            if (RequestRequiresTusResumableHeader(httpMethod))
            {
                if (context.Request.Headers.TusResumable == null)
                {
                    return IntentHandler.NotApplicable;
                }
            }

            if (MethodRequiresFileIdUrl(httpMethod))
            {
                if (!context.UrlHelper.UrlMatchesFileIdUrl(context))
                {
                    return IntentHandler.NotApplicable;
                }
            }
            else if (!context.UrlHelper.UrlMatchesUrlPath(context))
            {
                return IntentHandler.NotApplicable;
            }

            return httpMethod switch
            {
                "post" => DetermineIntentForPost(context),
                "patch" => DetermineIntentForPatch(context),
                "head" => DetermineIntentForHead(context),
                "options" => DetermineIntentForOptions(context),
                "delete" => DetermineIntentForDelete(context),
                _ => IntentHandler.NotApplicable,
            };
        }

        /// <summary>
        /// Returns the request method taking X-Http-Method-Override into account.
        /// </summary>
        /// <param name="request">The request to get the method for</param>
        /// <returns>The request method</returns>
        private static string GetHttpMethod(RequestAdapter request)
        {
            var method = request.Headers.XHttpMethodOveride;

            if (string.IsNullOrWhiteSpace(method))
            {
                method = request.Method;
            }

            return method.ToLower();
        }

        private static bool MethodRequiresFileIdUrl(string httpMethod)
        {
            return httpMethod switch
            {
                "head" or "patch" or "delete" => true,
                _ => false,
            };
        }

        private static IntentHandler DetermineIntentForOptions(ContextAdapter context)
        {
            return new GetOptionsHandler(context);
        }

        private static IntentHandler DetermineIntentForHead(ContextAdapter context)
        {
            return new GetFileInfoHandler(context);
        }

        private static IntentHandler DetermineIntentForPost(ContextAdapter context)
        {
            if (!context.StoreAdapter.Extensions.Creation)
                return IntentHandler.NotApplicable;

            var hasUploadConcatHeader = context.Request.Headers.ContainsKey(
                HeaderConstants.UploadConcat
            );

            // Client wants to use concatenation but the extension might not be available
            if (hasUploadConcatHeader)
            {
                return context.StoreAdapter.Extensions.Concatenation
                    ? new ConcatenateFilesHandler(context)
                    : IntentHandler.NotApplicable;
            }

            return new CreateFileHandler(context);
        }

        private static IntentHandler DetermineIntentForPatch(ContextAdapter context)
        {
            return new WriteFileHandler(context, initiatedFromCreationWithUpload: false);
        }

        private static IntentHandler DetermineIntentForDelete(ContextAdapter context)
        {
            if (!context.StoreAdapter.Extensions.Termination)
                return IntentHandler.NotApplicable;

            return new DeleteFileHandler(context);
        }

        private static bool RequestRequiresTusResumableHeader(string httpMethod)
        {
            return httpMethod != "options";
        }
    }
}
