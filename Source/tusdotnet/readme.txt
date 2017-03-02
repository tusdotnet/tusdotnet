How to use?

Create your Startup class as you would normally do. Add a using statement for `tusdotnet` and run `UseTus` on the app builder. 

app.UseTus(context => new DefaultTusConfiguration
{
	// c:\tusfiles is where to store files
	Store = new TusDiskStore(@"C:\tusfiles\"),
	// On what url should we listen for uploads?
	UrlPath = "/files",
	OnUploadCompleteAsync = (fileId, store, cancellationToken) =>
	{
		var file = await (store as ITusReadableStore)
        				.GetFileAsync(fileId, cancellationToken);
		await DoSomeProcessing(file);
		return Task.FromResult(true);
	}
});

You might also want to configure IIS (https://github.com/smatsson/tusdotnet/wiki/Configure-IIS) 
and/or configure CORS (https://github.com/smatsson/tusdotnet/wiki/Cross-domain-requests-(CORS)