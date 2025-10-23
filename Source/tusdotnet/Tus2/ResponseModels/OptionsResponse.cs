using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace tusdotnet.Tus2.ResponseModels
{
    internal class OptionsResponse : Tus2BaseResponse
    {
        private readonly TusHandlerLimits _limits;

        private static readonly TusHandlerLimits EMPTY_LIMITS = new() { MinSize = 0 };

        public OptionsResponse(TusHandlerLimits? limits)
        {
            Status = System.Net.HttpStatusCode.OK;
            _limits = limits;
        }

        protected override Task WriteResponse(HttpContext context)
        {
            var localLimits = _limits ?? EMPTY_LIMITS;

            context.SetHeader("Upload-Limit", localLimits.ToSfDictionary());
            context.SetHeader("Accept-Patch", "application/partial-upload");

            return Task.CompletedTask;
        }
    }
}
