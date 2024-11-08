#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Mvc;
using tusdotnet.ModelBinding.ModelBinders;

namespace tusdotnet.ModelBinding.Extensions
{
    public static class MvcOptionsExtensions
    {
        public static MvcOptions AddResumableUploadModelBinder(this MvcOptions options)
        {
            options.ModelBinderProviders.Insert(0, new MvcModelBinder());
            return options;
        }
    }
}
#endif
