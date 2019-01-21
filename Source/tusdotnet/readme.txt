How to use?

Create your Startup class as you would normally do. Add a using statement for `tusdotnet` and run `UseTus` on the app builder. 

app.UseTus(context => new DefaultTusConfiguration
{
	// c:\tusfiles is where to store files
	Store = new TusDiskStore(@"C:\tusfiles\"),
	// On what url should we listen for uploads?
	UrlPath = "/files",
    Events = new Events
    {
        OnFileCompleteAsync = async ctx =>
        {
            var file = await ctx.GetFileAsync();
		    await DoSomeProcessing(file);
        }
    }
});

More options and events are available on the wiki: https://github.com/tusdotnet/tusdotnet/wiki/Configuration