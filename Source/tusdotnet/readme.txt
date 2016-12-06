How to use?

Setup OWIN as you would normally do. Add a using statement for tusdotnet and run UseTus on the IAppBuilder.

app.UseTus(request => new DefaultTusConfiguration
{
	// c:\tusfiles is where to store files
	Store = new TusDiskStore(@"C:\tusfiles\"),
	// On what url should we listen for uploads?
	UrlPath = "/files",
	OnUploadCompleteAsync = (fileId, store, cancellationToken) =>
	{
		var file = await (store as ITusReadableStore).GetFileAsync(fileId, cancellationToken);
		await DoSomeProcessing(file);
		return Task.FromResult(true);
	}
});

If you just want to play around with the protocol, clone the repo and run the OwinTestApp. 
It launches a small site running tusdotnet and the official JS client so that you can test the protocol on your own machine.