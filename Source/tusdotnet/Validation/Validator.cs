using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;

namespace tusdotnet.Validation
{
    internal sealed class Validator
    {
        public HttpStatusCode StatusCode { get; private set; }
        public string ErrorMessage { get; private set; }

        private readonly Requirement[] _requirements;

        public Validator(Requirement[] requirements)
        {
            _requirements = requirements;
        }

        public async Task Validate(ContextAdapter context)
        {
            StatusCode = HttpStatusCode.OK;
            ErrorMessage = null;

            foreach (var spec in _requirements)
            {
                spec.Reset();
                await spec.Validate(context);

                if (spec.StatusCode == 0)
                {
                    continue;
                }

                StatusCode = spec.StatusCode;
                ErrorMessage = spec.ErrorMessage;
                break;
            }
        }
    }
}