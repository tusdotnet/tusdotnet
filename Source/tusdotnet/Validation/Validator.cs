using System.Net;
using tusdotnet.Adapters;

namespace tusdotnet.Validation
{
    internal class Validator
    {
        public HttpStatusCode StatusCode { get; private set; }
        public string ErrorMessage { get; private set; }

        private readonly Specification[] _specifications;

        public Validator(params Specification[] specs)
        {
            _specifications = specs ?? new Specification[0];
        }

        public void Validate(ContextAdapter context)
        {
            StatusCode = HttpStatusCode.OK;
            ErrorMessage = null;

            foreach (var spec in _specifications)
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