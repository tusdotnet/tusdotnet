#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Mvc;

namespace tusdotnet.ModelBinders
{
    public static class TusMvcOptionsExtensions
    {
        public static MvcOptions AddResumableUploadModelBinder(this MvcOptions options)
        {
            options.ModelBinderProviders.Insert(0, new MvcModelBinderProvider());
            return options;
        }
    }
}
#endif