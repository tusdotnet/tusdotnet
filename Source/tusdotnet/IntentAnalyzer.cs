using System;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions.Internal;
using tusdotnet.IntentHandlers;
using tusdotnet.Interfaces;

namespace tusdotnet
{
    internal static class IntentAnalyzer
    {
        public static IntentHandler DetermineIntent(ContextAdapter context)
        {
            var httpMethod = context.Request.GetHttpMethod();

            if (RequestRequiresTusResumableHeader(httpMethod))
            {
                if (context.Request.GetHeader(HeaderConstants.TusResumable) == null)
                {
                    return IntentHandler.NotApplicable;
                }
            }

            // TODO: Hack, fix in a better way
            if (httpMethod == "head" && context.Configuration.SupportsClientTag())
            {
                if (!UrlMatchesFileIdUrl(context.Request.RequestUri, context.Configuration.UrlPath) && !UrlMatchesUrlPath(context.Request.RequestUri, context.Configuration.UrlPath))
                {
                    return IntentHandler.NotApplicable;
                }
            }
            else if (MethodRequiresFileIdUrl(httpMethod))
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

            var clientTagStore = context.Configuration.SupportsClientTag() ? (ITusClientTagStore)context.Configuration.Store : null;

            var challengeStore = context.Configuration.Store as ITusChallengeStore;

            if (context.Configuration.Store is ITusConcatenationStore tusConcatenationStore
                && hasUploadConcatHeader)
            {
                return new ConcatenateFilesHandler(context, tusConcatenationStore, clientTagStore, challengeStore);
            }

            return new CreateFileHandler(context, creationStore, clientTagStore, challengeStore);
        }

        private static IntentHandler DetermineIntentForPatch(ContextAdapter context)
        {
            return new WriteFileHandler(context, initiatedFromCreationWithUpload: false);
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
            return requestUri.LocalPath.TrimEnd('/').Equals(configUrlPath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        private static bool UrlMatchesFileIdUrl(Uri requestUri, string configUrlPath)
        {
            return !UrlMatchesUrlPath(requestUri, configUrlPath)
                   && requestUri.LocalPath.StartsWith(configUrlPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
