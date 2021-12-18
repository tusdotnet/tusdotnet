using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTus(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<Tus2Options>(configuration);
            services.AddSingleton<IUploadTokenParser, UploadTokenParser>();
            services.AddSingleton<IMetadataParser, MetadataParser>();
            return services;
        }
    }
}
