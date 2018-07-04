using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace AspNetCore_netcoreapp2_1_TestApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            return new WebHostBuilder()
                   .UseKestrel(options =>
                   {
                       options.Limits.MaxRequestBufferSize = null;
                       options.Limits.MaxRequestBodySize = null;
                   })
                   .UseContentRoot(Directory.GetCurrentDirectory())
                   .UseIISIntegration()
                   .UseStartup<Startup>();
        }
    }
}
