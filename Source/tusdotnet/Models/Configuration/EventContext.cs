using System.Threading;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;

namespace tusdotnet.Models.Configuration
{
    namespace tusdotnet.Models.Configuration
    {
        /// <summary>
        /// Base context for all events in tusdotnet
        /// </summary>
        public abstract class EventContext
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

            internal static T FromContext<T>(ContextAdapter context) where T : EventContext, new()
            {
                return new T
                {
                    Store = context.Configuration.Store,
                    CancellationToken = context.CancellationToken,
                    FileId = context.GetFileId()
                };
            }
        }
    }
}
