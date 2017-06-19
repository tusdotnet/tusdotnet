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
            var request = context.Request;
            var startIndex = request
                                 .RequestUri
                                 .LocalPath
                                 .IndexOf(context.Configuration.UrlPath, StringComparison.OrdinalIgnoreCase) + context.Configuration.UrlPath.Length;
            return request
                .RequestUri
                .LocalPath
                .Substring(startIndex)
                .Trim('/');
        }

        public static bool IsExactUrlMatch(this ContextAdapter context)
        {
            return context.Request.RequestUri.LocalPath.TrimEnd('/') == context.Configuration.UrlPath.TrimEnd('/');
        }

        internal static IList<string> DetectExtensions(this ContextAdapter context)
        {
            var extensions = new List<string>();
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
