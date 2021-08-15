using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Helpers;

namespace tusdotnet.Validation
{
    internal abstract class Requirement
    {
        public HttpStatusCode StatusCode { get; protected set; }

        public string ErrorMessage { get; protected set; }

        public abstract Task Validate(ContextAdapter context);

        public void Reset()
        {
            StatusCode = 0;
            ErrorMessage = null;
        }

        protected Task Conflict(string errorMessage)
        {
            return Error(HttpStatusCode.Conflict, errorMessage);
        }

        protected Task BadRequest(string errorMessage)
        {
            return Error(HttpStatusCode.BadRequest, errorMessage);
        }

        protected Task RequestEntityTooLarge(string errorMessage)
        {
            return Error(HttpStatusCode.RequestEntityTooLarge, errorMessage);
        }

        protected Task Forbidden(string errorMessage)
        {
            return Error(HttpStatusCode.Forbidden, errorMessage);
        }

        protected Task NotFound()
        {
            return Error(HttpStatusCode.NotFound, null);
        }

        protected Task UnsupportedMediaType(string errorMessage)
        {
            return Error(HttpStatusCode.UnsupportedMediaType, errorMessage);
        }

        protected Task Error(HttpStatusCode status, string errorMessage)
        {
            StatusCode = status;
            ErrorMessage = errorMessage;
            return TaskHelper.Completed;
        }
    }
}
