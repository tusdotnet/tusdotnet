using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using System.Reflection;

namespace tusdotnet.Helpers
{
    internal static class EventHelper
    {
        internal static async Task<ResultType> Validate<T>(ContextAdapter context, Action<T> configure = null) where T : ValidationContext<T>, new()
        {
            var handler = GetHandler<T>(context);

            if (handler == null)
                return ResultType.ContinueExecution;

            var eventContext = EventContext<T>.Create(context, configure);

            await handler.Invoke(eventContext);

            if (eventContext.HasFailed)
            {
                await context.Response.Error(eventContext.StatusCode, eventContext.ErrorMessage);
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
            if (context.Configuration.OnUploadCompleteAsync == null && context.Configuration.Events?.OnFileCompleteAsync == null)
            {
                return;
            }

            var eventContext = FileCompleteContext.Create(context, configure);

            if (context.Configuration.OnUploadCompleteAsync != null)
            {
                await context.Configuration.OnUploadCompleteAsync.Invoke(eventContext.FileId, eventContext.Store, eventContext.CancellationToken);
            }

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

            var handlerProperty = typeof(Events).GetProperties().FirstOrDefault(f => f.Name == TypeToEventName<T>());

            var handler = handlerProperty.GetValue(context.Configuration.Events) as Func<T, Task>;
            return handler;
        }

        private static string TypeToEventName<T>() where T : EventContext<T>, new()
        {
            var eventContextName = typeof(T).Name;
            var eventNameWithoutContext = eventContextName.Substring(0, eventContextName.LastIndexOf("Context"));
            return "On" + eventNameWithoutContext + "Async";
        }
    }
}