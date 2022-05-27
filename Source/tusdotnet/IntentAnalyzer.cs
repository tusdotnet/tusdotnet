using System;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.IntentHandlers;
using tusdotnet.Interfaces;

namespace tusdotnet
{
    internal static class IntentAnalyzer
    {
        public static IntentHandler DetermineIntent(ContextAdapter context)
        {
            var httpMethod = GetHttpMethod(context.Request);

            if (RequestRequiresTusResumableHeader(httpMethod))
            {
                if (context.Request.Headers.TusResumable == null)
                {
                    return IntentHandler.NotApplicable;
                }
            }

            // TODO: Optimize for endpoint routing
            if (MethodRequiresFileIdUrl(httpMethod))
            {
                if (!UrlMatchesFileIdUrl(context.Request.RequestUri, context.ConfigUrlPath))
                {
                    return IntentHandler.NotApplicable;
                }
            }
            else if (!UrlMatchesUrlPath(context.Request.RequestUri, context.ConfigUrlPath))
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

            var hasUploadConcatHeader = context.Request.Headers.ContainsKey(HeaderConstants.UploadConcat);

            if (context.StoreAdapter.Extensions.Concatenation && hasUploadConcatHeader)
            {
                return new ConcatenateFilesHandler(context);
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
