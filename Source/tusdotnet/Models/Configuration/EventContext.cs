using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using tusdotnet.Adapters;
using tusdotnet.Interfaces;
#if netfull
using Microsoft.Owin;
#endif

namespace tusdotnet.Models.Configuration
{
    /// <summary>
    /// Base context for all events in tusdotnet
    /// </summary>
    /// <typeparam name="TSelf">The type of the derived class inheriting the EventContext</typeparam>
    public abstract class EventContext<TSelf>
        where TSelf : EventContext<TSelf>, new()
    {
        // Cache completed null task to avoid allocation on every null FileId call
        private static readonly Task<ITusFile> s_completedNullFileTask = Task.FromResult<ITusFile>(
            null
        );

        /// <summary>
        /// The id of the file that was completed
        /// </summary>
        public string FileId { get; set; }

        /// <summary>
        /// The store that was used when completing the upload
        /// </summary>
        public ITusStore Store { get; set; }

        /// <summary>
        /// The request's cancellation token
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

#if netfull

        /// <summary>
        /// The OWIN context for the current request
        /// </summary>
        public IOwinContext OwinContext { get; private set; }
#endif

        /// <summary>
        /// The http context for the current request
        /// </summary>
        public HttpContext HttpContext { get; private set; }

        /// <summary>
        /// Get the file with the id specified in the <see cref="FileId"/> property.
        /// Returns null if there is no file id or if the file was not found.
        /// </summary>
        /// <returns>The file or null</returns>
        public Task<ITusFile> GetFileAsync()
        {
            if (string.IsNullOrEmpty(FileId))
                return s_completedNullFileTask;

            return ((ITusReadableStore)Store).GetFileAsync(FileId, CancellationToken);
        }

        internal static TSelf Create(ContextAdapter context, Action<TSelf> configure = null)
        {
            var fileId = string.IsNullOrEmpty(context.FileId) ? null : context.FileId;

            var eventContext = new TSelf
            {
                Store = context.Configuration.Store,
                CancellationToken = context.CancellationToken,
                FileId = fileId,
                HttpContext = context.HttpContext,
#if netfull
                OwinContext = context.OwinContext
#endif
            };

            configure?.Invoke(eventContext);

            return eventContext;
        }
    }
}
