namespace tusdotnet.Interfaces
{
	public interface ITusConfiguration
	{
		string UrlPath { get; }
		ITusStore Store { get; }
	}
}