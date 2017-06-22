using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Helpers;
using tusdotnet.ProtocolHandlers;

namespace tusdotnet
{
    internal static class TusProtocolHandler
    {
        public static async Task<bool> Invoke(ContextAdapter context)
        {
            context.Configuration.Validate();

            var request = context.Request;
            var response = context.Response;

            var methodHandler = GetProtocolMethodHandler(context);

            if (methodHandler == null)
            {
                return false;
            }

            var tusResumable = request.Headers.ContainsKey(HeaderConstants.TusResumable)
                ? request.Headers[HeaderConstants.TusResumable].FirstOrDefault()
                : null;

            if (!(methodHandler is OptionsHandler))
            {
                if (!request.Headers.ContainsKey(HeaderConstants.TusResumable))
                {
                    return false;
                }

                if (tusResumable != HeaderConstants.TusResumableValue)
                {
                    response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
                    response.SetHeader(HeaderConstants.TusVersion, HeaderConstants.TusResumableValue);
                    return await response.Error(HttpStatusCode.PreconditionFailed,
                        $"Tus version {tusResumable} is not supported. Supported versions: {HeaderConstants.TusResumableValue}");
                }
            }
            
            FileLock fileLock = null;

            if (methodHandler.RequiresLock)
            {
                fileLock = new FileLock(context.GetFileId());

                var hasLock = fileLock.Lock(context.CancellationToken);
                if (!hasLock)
                {
                    return await response.Error(HttpStatusCode.Conflict,
                        $"File {context.GetFileId()} is currently being updated. Please try again later");
                }
            }

            try
            {
                if (!await methodHandler.Validate(context))
                {
                    return true;
                }

                return await methodHandler.Handle(context);
            }
            finally
            {
                fileLock?.ReleaseIfHeld();
            }
        }

        private static ProtocolMethodHandler GetProtocolMethodHandler(ContextAdapter context)
        {
            if (!MethodHandlerFactories.TryGetValue(context.Request.GetMethod(), out Func<ProtocolMethodHandler> factory))
            {
                return null;
            }

            var handler = factory();
            return handler.CanHandleRequest(context) ? handler : null;
        }

        private static readonly Dictionary<string, Func<ProtocolMethodHandler>> MethodHandlerFactories =
            new Dictionary<string, Func<ProtocolMethodHandler>>
            {
                {"post", () => new PostHandler()},
                {"head", () => new HeadHandler()},
                {"patch", () => new PatchHandler()},
                {"options", () => new OptionsHandler()},
                {"delete", () => new DeleteHandler()}
            };
    }
}