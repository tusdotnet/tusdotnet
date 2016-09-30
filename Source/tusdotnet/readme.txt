How to use?

Setup OWIN as you would normally do. Add a using statement for tusdotnet and run UseTus on the IAppBuilder.

app.UseTus(() => new DefaultTusConfiguration
			{
				Store = new TusDiskStore(@"C:\tusfiles\"),
				UrlPath = "/files",
				OnUploadCompleteAsync = (fileId, store, cancellationToken) =>
				{
					Console.WriteLine($"Upload of {fileId} is complete. Callback also got a store of type {store.GetType().FullName}");
					// If the store implements ITusReadableStore one could access the completed file here.
					// The default TusDiskStore implements this interface:
					// var file = await (store as ITusReadableStore).GetFileAsync(fileId, cancellationToken);
					return Task.FromResult(true);
				}
			});

If you just want to play around with the protocol, clone the repo and run the OwinTestApp. 
It launches a small site running tusdotnet and the official JS client so that you can test the protocol on your own machine.