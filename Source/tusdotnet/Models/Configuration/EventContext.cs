using System;
using System.Threading;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;
using Microsoft.AspNetCore.Http;
#if netfull
using Microsoft.Owin;
#endif

namespace tusdotnet.Models.Configuration
{
    /// <summary>
    /// Base context for all events in tusdotnet
    /// </summary>
    /// <typeparam name="TSelf">The type of the derived class inheriting the EventContext</typeparam>
    public abstract class EventContext<TSelf> where TSelf : EventContext<TSelf>, new()
    {
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

        public IOwinContext OwinContext { get; private set; }

#endif

        public HttpContext HttpContext { get; private set; }

        internal static TSelf Create(ContextAdapter context, Action<TSelf> configure = null)
        {
            var fileId = context.GetFileId();
            if (string.IsNullOrEmpty(fileId))
            {
                fileId = null;
            }

            var eventContext = new TSelf
            {
                Store = context.Configuration.Store,
                CancellationToken = context.CancellationToken,
                FileId = fileId,
                HttpContext = context.HttpContext as HttpContext,
#if netfull
                OwinContext = context.HttpContext as IOwinContext
#endif
            };

            configure?.Invoke(eventContext);

            return eventContext;
        }
    }
}
