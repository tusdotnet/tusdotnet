using System;
using System.Threading;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
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
            if(string.IsNullOrEmpty(FileId))
                return Task.FromResult<ITusFile>(null);

            return ((ITusReadableStore)Store).GetFileAsync(FileId, CancellationToken);
        }

        internal static TSelf Create(ContextAdapter context, Action<TSelf> configure = null)
        {
            var fileId = context.Request.FileId;
            if (string.IsNullOrEmpty(fileId))
            {
                fileId = null;
            }

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
