#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Threading.Tasks;

namespace tusdotnet.ModelBinders
{
    internal class MvcModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            if (typeof(ResumableUpload).IsAssignableFrom(context.Metadata.ModelType))
                return new MvcModelBinder();

            return null;
        }
    }

    internal class MvcModelBinder : IModelBinder
    {
        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var obj = await ResumableUpload.CreateAndBindFromHttpContext(bindingContext.ModelMetadata.ModelType, bindingContext.HttpContext);

            bindingContext.Result = obj is null
                ? ModelBindingResult.Failed()
                : ModelBindingResult.Success(obj);
        }
    }
}
#endif