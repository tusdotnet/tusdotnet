using System.Net;
using tusdotnet.Adapters;

namespace tusdotnet.Validation
{
    internal sealed class Validator
    {
        public HttpStatusCode StatusCode { get; private set; }
        public string ErrorMessage { get; private set; }

        private readonly Requirement[] _requirements;

        public Validator(params Requirement[] requirements)
        {
            _requirements = requirements ?? new Requirement[0];
        }

        public void Validate(ContextAdapter context)
        {
            StatusCode = HttpStatusCode.OK;
            ErrorMessage = null;

            foreach (var spec in _requirements)
            {
                spec.Reset();
                spec.Validate(context);
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