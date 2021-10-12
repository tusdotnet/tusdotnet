using System;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;

namespace tusdotnet.Helpers
{
    internal static class EventHelper
    {
        internal static async Task<ResultType> Validate<T>(ContextAdapter context, Action<T> configure = null) where T : ValidationContext<T>, new()
        {
            var handler = GetHandlerFromEvents<T>(context.Configuration.Events);

            if (handler == null)
                return ResultType.ContinueExecution;

            var eventContext = EventContext<T>.Create(context, configure);

            await handler(eventContext);

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
            var handler = GetHandlerFromEvents<T>(context.Configuration.Events);

            if (handler == null)
            {
                return;
            }

            var eventContext = EventContext<T>.Create(context, configure);

            await handler(eventContext);
        }

        private static Func<T, Task> GetHandlerFromEvents<T>(Events events) where T : EventContext<T>, new()
        {
            if (events == null)
            {
                return null;
            }

            var t = typeof(T);

            if (t == typeof(AuthorizeContext))
                return (Func<T, Task>)events.OnAuthorizeAsync;

            if (t == typeof(BeforeCreateContext))
                return (Func<T, Task>)events.OnBeforeCreateAsync;

            if (t == typeof(CreateCompleteContext))
                return (Func<T, Task>)events.OnCreateCompleteAsync;

            if (t == typeof(BeforeDeleteContext))
                return (Func<T, Task>)events.OnBeforeDeleteAsync;

            if (t == typeof(DeleteCompleteContext))
                return (Func<T, Task>)events.OnDeleteCompleteAsync;

            return null;
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
                await context.Configuration.OnUploadCompleteAsync(eventContext.FileId, eventContext.Store, eventContext.CancellationToken);
            }
#pragma warning restore CS0618 // Type or member is obsolete

            if (context.Configuration.Events?.OnFileCompleteAsync != null)
            {
                await context.Configuration.Events.OnFileCompleteAsync(eventContext);
            }
        }
    }
}