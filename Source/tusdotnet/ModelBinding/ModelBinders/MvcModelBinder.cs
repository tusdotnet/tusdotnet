#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using tusdotnet.ModelBinding.Models;

namespace tusdotnet.ModelBinding.ModelBinders
{
    internal class MvcModelBinder : IModelBinderProvider, IModelBinder
    {
        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var obj = await ResumableUpload.CreateAndBindFromHttpContext(
                bindingContext.ModelMetadata.ModelType,
                bindingContext.HttpContext
            );

            bindingContext
                .HttpContext.RequestServices.GetRequiredService<ILogger<MvcModelBinder>>()
                .LogInformation("Binding model");

            bindingContext.Result = obj is null
                ? ModelBindingResult.Failed()
                : ModelBindingResult.Success(obj);
        }

        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            if (typeof(ResumableUpload).IsAssignableFrom(context.Metadata.ModelType))
                return this;

            return null;
        }
    }
}
#endif
