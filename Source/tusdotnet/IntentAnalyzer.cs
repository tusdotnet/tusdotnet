using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.IntentHandlers;
using tusdotnet.Interfaces;

namespace tusdotnet
{
    internal static class IntentAnalyzer
    {
        public static async Task<IntentHandler> DetermineIntent(ContextAdapter context)
        {
            var httpMethod = context.Request.GetMethod();

            if (httpMethod != "options")
            {
                var tusResumableHeaderIsProvidedAndValid = await ValidateTusResumableHeader(context);

                if (!tusResumableHeaderIsProvidedAndValid)
                    return IntentHandler.NotApplicable;
            }

            switch (httpMethod)
            {
                case "post":
                    return DetermineIntentForPost(context);
                case "patch":
                    return DetermineIntentForPatch(context);
                case "head":
                    return DetermineIntentForHead(context);
                case "options":
                    return DetermineIntentForOptions(context);
                case "delete":
                    return DetermineIntentForDelete(context);
                default:
                    return IntentHandler.NotApplicable;
            }
        }

        private static async Task<bool> ValidateTusResumableHeader(ContextAdapter context)
        {
            var tusResumable = context.Request.Headers.ContainsKey(HeaderConstants.TusResumable)
                    ? context.Request.GetHeader(HeaderConstants.TusResumable)
                    : null;

            if (tusResumable == null)
            {
                return false;
            }

            if (tusResumable != HeaderConstants.TusResumableValue)
            {
                context.Response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
                context.Response.SetHeader(HeaderConstants.TusVersion, HeaderConstants.TusResumableValue);
                await context.Response.Error(HttpStatusCode.PreconditionFailed,
                    $"Tus version {tusResumable} is not supported. Supported versions: {HeaderConstants.TusResumableValue}");
                return false;
            }

            return true;
        }

        private static IntentHandler DetermineIntentForOptions(ContextAdapter context)
        {
            if (!context.UrlMatchesUrlPath())
                return IntentHandler.NotApplicable;

            return new GetOptionsHandler(context);
        }

        private static IntentHandler DetermineIntentForHead(ContextAdapter context)
        {
            if (!context.UrlMatchesFileIdUrl())
                return IntentHandler.NotApplicable;

            return new GetFileInfoHandler(context);
        }

        private static IntentHandler DetermineIntentForPost(ContextAdapter context)
        {
            if (!context.UrlMatchesUrlPath())
                return IntentHandler.NotApplicable;

            if (!(context.Configuration.Store is ITusCreationStore creationStore))
                return IntentHandler.NotApplicable;

            var hasUploadConcatHeader = context.Request.Headers.ContainsKey(HeaderConstants.UploadConcat);
            var isSupportedConcatRequest = context.Configuration.Store is ITusConcatenationStore tusConcatenationStore && hasUploadConcatHeader;

            if (isSupportedConcatRequest)
            {
                return new ConcatenateFilesHandler(context);
            }

            return new CreateFileHandler(context, creationStore);
        }

        private static IntentHandler DetermineIntentForPatch(ContextAdapter context)
        {
            if (!context.UrlMatchesFileIdUrl())
                return IntentHandler.NotApplicable;

            return new WriteFileHandler(context);
        }

        private static IntentHandler DetermineIntentForDelete(ContextAdapter context)
        {
            if (!context.UrlMatchesFileIdUrl())
                return IntentHandler.NotApplicable;

            if (!(context.Configuration.Store is ITusTerminationStore terminationStore))
                return IntentHandler.NotApplicable;

            return new DeleteFileHandler(context, terminationStore);
        }
    }
}
