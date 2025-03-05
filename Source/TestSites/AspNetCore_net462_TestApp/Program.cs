using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace AspNetCore_Net462_TestApp
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost
                .CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseKestrel(options =>
                {
                    options.Limits.MaxRequestBodySize = null;
                    options.Limits.MaxRequestBufferSize = null;
                })
                .Build();
    }
}
