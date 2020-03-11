using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace AspNetCore_netcoreapp1_1_TestApp
{
    public static class Program
    {
        public static void Main()
        {
            var host = new WebHostBuilder()
                .UseKestrel(options => options.Limits.MaxRequestBufferSize = null)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
