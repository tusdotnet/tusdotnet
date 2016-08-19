using tusdotnet.Interfaces;

namespace tusdotnet.Models
{
	public class DefaultTusConfiguration : ITusConfiguration
	{
		public string UrlPath { get; set; }
		public ITusStore Store { get; set; }
	}
}
