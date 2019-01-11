using System;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.IntentHandlers;
using tusdotnet.Interfaces;

namespace tusdotnet
{
    internal static class IntentAnalyzer
    {
        public static IntentHandler DetermineIntent(ContextAdapter context)
        {
            var httpMethod = context.Request.GetMethod();

            if (RequestRequiresTusResumableHeader(httpMethod)
               && context.Request.GetHeader(HeaderConstants.TusResumable) == null)
            {
                return IntentHandler.NotApplicable;
            }

#warning TODO: Possibly move this to RequestIsForTusEndpoint to minimize allocations
            if (MethodRequiresFileIdUrl(httpMethod))
            {
                if (!UrlMatchesFileIdUrl(context.Request.RequestUri, context.Configuration.UrlPath))
                {
                    return IntentHandler.NotApplicable;
                }
            }
            else if (!UrlMatchesUrlPath(context.Request.RequestUri, context.Configuration.UrlPath))
            {
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

        private static bool MethodRequiresFileIdUrl(string httpMethod)
        {
            switch (httpMethod)
            {
                case "head":
                case "patch":
                case "delete":
                    return true;
                default:
                    return false;
            }
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
            if (!(context.Configuration.Store is ITusCreationStore creationStore))
                return IntentHandler.NotApplicable;

            var hasUploadConcatHeader = context.Request.Headers.ContainsKey(HeaderConstants.UploadConcat);

            if (context.Configuration.Store is tusdotnet.Interfaces.ITusConcatenationStore tusConcatenationStore
                && hasUploadConcatHeader)
            {
                return new ConcatenateFilesHandler(context, tusConcatenationStore);
            }

            return new CreateFileHandler(context, creationStore);
        }

        private static IntentHandler DetermineIntentForPatch(ContextAdapter context)
        {
            return new WriteFileHandler(context);
        }

        private static IntentHandler DetermineIntentForDelete(ContextAdapter context)
        {
            if (!(context.Configuration.Store is ITusTerminationStore terminationStore))
                return IntentHandler.NotApplicable;

            return new DeleteFileHandler(context, terminationStore);
        }

        private static bool RequestRequiresTusResumableHeader(string httpMethod)
        {
            return httpMethod != "options";
        }

        private static bool UrlMatchesUrlPath(Uri requestUri, string configUrlPath)
        {
            return requestUri.LocalPath.TrimEnd('/') == configUrlPath.TrimEnd('/');
        }

        private static bool UrlMatchesFileIdUrl(Uri requestUri, string configUrlPath)
        {
            return !UrlMatchesUrlPath(requestUri, configUrlPath)
                   && requestUri.LocalPath.StartsWith(configUrlPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
