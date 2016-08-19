using System;

namespace OwinTestApp
{
	class Program
	{
		static void Main(string[] args)
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
