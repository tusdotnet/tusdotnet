using System;
using System.Collections.Generic;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Interfaces;

namespace tusdotnet.Extensions
{
    internal static class ContextAdapterExtensions
    {
        public static string GetFileId(this ContextAdapter context)
        {
            var startIndex =
                context.Request.RequestUri.LocalPath.IndexOf(context.Configuration.UrlPath,
                    StringComparison.OrdinalIgnoreCase) + context.Configuration.UrlPath.Length;

            return context.Request
                .RequestUri
                .LocalPath
                .Substring(startIndex)
                .Trim('/');
        }

        [Obsolete("Move to IntentAnalyzer")]
        public static bool UrlMatchesUrlPath(this ContextAdapter context)
        {
            return context.Request.RequestUri.LocalPath.TrimEnd('/') == context.Configuration.UrlPath.TrimEnd('/');
        }

        [Obsolete("Move to IntentAnalyzer")]
        public static bool UrlMatchesFileIdUrl(this ContextAdapter context)
        {
            return !context.UrlMatchesUrlPath()
                   && context.Request.RequestUri.LocalPath.StartsWith(context.Configuration.UrlPath,
                       StringComparison.OrdinalIgnoreCase);
        }

        [Obsolete("Move to GetOptionsHandler")]
        internal static List<string> DetectExtensions(this ContextAdapter context)
        {
            var extensions = new List<string>(6);
            if (context.Configuration.Store is ITusCreationStore)
            {
                extensions.Add(ExtensionConstants.Creation);
            }

            if (context.Configuration.Store is ITusTerminationStore)
            {
                extensions.Add(ExtensionConstants.Termination);
            }

            if (context.Configuration.Store is ITusChecksumStore)
            {
                extensions.Add(ExtensionConstants.Checksum);
            }

            if (context.Configuration.Store is ITusConcatenationStore)
            {
                extensions.Add(ExtensionConstants.Concatenation);
            }

            if (context.Configuration.Store is ITusExpirationStore)
            {
                extensions.Add(ExtensionConstants.Expiration);
            }

            if (context.Configuration.Store is ITusCreationDeferLengthStore)
            {
                extensions.Add(ExtensionConstants.CreationDeferLength);
            }

            return extensions;
        }
    }
}