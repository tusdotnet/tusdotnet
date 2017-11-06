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
        OnFileCompleteAsync = ctx =>
        {
            var file = await ((ITusReadableStore)ctx.Store).GetFileAsync(ctx.FileId, ctx.CancellationToken);
		    await DoSomeProcessing(file);
        }
    }
});

You might also want to configure IIS (https://github.com/tusdotnet/tusdotnet/wiki/Configure-IIS) 
and/or configure CORS (https://github.com/tusdotnet/tusdotnet/wiki/Cross-domain-requests-(CORS)