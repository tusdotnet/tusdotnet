using System;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using System.Reflection;
using System.Collections.Generic;

namespace tusdotnet.Helpers
{
    internal static class EventHelper
    {
        private static readonly Lazy<Dictionary<Type, PropertyInfo>> _eventHandlers = new Lazy<Dictionary<Type, PropertyInfo>>(GatherEventHandlers);

        internal static async Task<ResultType> Validate<T>(ContextAdapter context, Action<T> configure = null) where T : ValidationContext<T>, new()
        {
            var handler = GetHandler<T>(context);

            if (handler == null)
                return ResultType.ContinueExecution;

            var eventContext = EventContext<T>.Create(context, configure);

            await handler.Invoke(eventContext);

            if (eventContext.HasFailed)
            {
                var includeTusResumableHeaderInResponse = true;
                // Do not leak information on the tus endpoint during authorization.
                if (eventContext is EventContext<AuthorizeContext>)
                {
                    includeTusResumableHeaderInResponse = false;
                }
                await context.Response.Error(eventContext.StatusCode, eventContext.ErrorMessage, includeTusResumableHeaderInResponse);
                return ResultType.StopExecution;
            }

            return ResultType.ContinueExecution;
        }

        internal static async Task Notify<T>(ContextAdapter context, Action<T> configure = null) where T : EventContext<T>, new()
        {
            var handler = GetHandler<T>(context);

            if (handler == null)
            {
                return;
            }

            var eventContext = EventContext<T>.Create(context, configure);

            await handler.Invoke(eventContext);
        }

        internal static async Task NotifyFileComplete(ContextAdapter context, Action<FileCompleteContext> configure = null)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if (context.Configuration.OnUploadCompleteAsync == null && context.Configuration.Events?.OnFileCompleteAsync == null)
            {
                return;
            }

            var eventContext = FileCompleteContext.Create(context, configure);

            if (context.Configuration.OnUploadCompleteAsync != null)

            {
                await context.Configuration.OnUploadCompleteAsync.Invoke(eventContext.FileId, eventContext.Store, eventContext.CancellationToken);
            }
#pragma warning restore CS0618 // Type or member is obsolete

            if (context.Configuration.Events?.OnFileCompleteAsync != null)
            {
                await context.Configuration.Events.OnFileCompleteAsync(eventContext);
            }
        }

        private static Func<T, Task> GetHandler<T>(ContextAdapter context) where T : EventContext<T>, new()
        {
            if (context.Configuration.Events == null)
            {
                return null;
            }

            var handlerProperty = _eventHandlers.Value[typeof(T)];

            var handler = handlerProperty.GetValue(context.Configuration.Events) as Func<T, Task>;
            return handler;
        }

        private static Dictionary<Type, PropertyInfo> GatherEventHandlers()
        {
            var result = new Dictionary<Type, PropertyInfo>();
            var properties = typeof(Events).GetProperties();
            foreach (var item in properties)
            {
                var types = item.PropertyType.GenericTypeArguments;
                result.Add(types[0], item);
            }

            return result;
        }
    }
}