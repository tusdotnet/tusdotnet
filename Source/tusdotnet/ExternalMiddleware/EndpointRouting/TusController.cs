#if endpointrouting

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace tusdotnet.ExternalMiddleware.EndpointRouting
{
    public abstract class TusController<TConfigurator> where TConfigurator : ITusConfigurator
    {
        protected TusController(StorageService<TConfigurator> storage)
        {
            Storage = storage;
        }

        public StorageService<TConfigurator> Storage { get; }

        public virtual async Task<IActionResult> FileCompleted(FileCompletedContext context, CancellationToken cancellation) { return new OkResult(); }

        public virtual async Task<IActionResult> Create(CreateContext context, CancellationToken cancellation)
        {
            await Storage.Create(context, cancellation);
            return Ok();
        }

        public virtual async Task<IActionResult> Write(WriteContext context, CancellationToken cancellationToken)
        {
            await Storage.Write(context, cancellationToken);
            return Ok();
        }

        internal async Task<bool> AuthorizeForAction(HttpContext context, string actionName)
        {
            var authService = context.RequestServices.GetService<IAuthorizationService>();
            if (authService != null)
            {
                var authorizeAttribute = GetType().GetMethod(actionName).GetCustomAttributes(false).OfType<AuthorizeAttribute>().FirstOrDefault();

                var authResult = await authService.AuthorizeAsync(context.User, authorizeAttribute.Policy);
                return authResult.Succeeded;
            }

            return true;
        }

        private IActionResult Ok() => new OkResult();
    }
}

#endif