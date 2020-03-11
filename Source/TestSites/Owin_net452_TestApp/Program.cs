using System;

namespace OwinTestApp
{
    public static class Program
    {
        public static void Main()
        {
            const string url = "http://localhost:9000";
            using (Microsoft.Owin.Hosting.WebApp.Start<Startup>(url))
            {
                Console.WriteLine($"Server running at {url}");
                Console.WriteLine("Press [enter] to quit...");
                Console.ReadLine();
            }
        }
    }
}
