# tusdotnet
.NET server implementation of the Tus protocol for resumable file uploads. Read more at http://tus.io

## What?
From tus.io:
>tus is a new open protocol for resumable uploads built on HTTP. It offers simple, cheap and reusable stacks for clients and servers. It supports any language, any platform and any network.

tusdotnet is a .NET server implementation of the Tus protocol. It is written as a OWIN middleware for easy usage.

Comments, ideas, questions and PRs are welcome :)

## Features
* Supports Tus 1.0.0 core protocol and the Creation extension
* Easy to use OWIN middleware
* Fast and reliable
* 99% test coverage
* MIT licensed

## How to use
Clone this repository and compile using Visual Studio 2015. Include `tusdotnet.dll` in your project (or just include the source code).

Setup OWIN as you would normally do. Add a using statement for `tusdotnet` and run UseTus on the IAppBuilder.

```csharp
app.UseTus(() => new DefaultTusConfiguration
			{
				Store = new TusDiskStore(@"C:\tusfiles\"),
				UrlPath = "/files",
				OnUploadCompleteAsync = (fileId, store, cancellationToken) =>
				{
					// Called when a file upload is completed.
					// If the store implements ITusReadableStore one could access 
                    // the completed file here. 
                    // The default TusDiskStore implements this interface:
					// var file = await 
                    //		(store as ITusReadableStore)
                    //			.GetFileAsync(fileId, cancellationToken);
					return Task.FromResult(true);
				}
			});
  ```
 
If you just want to play around with the protocol, clone the repo and run the OwinTestApp. It launches a small site running tusdotnet and the [official JS client](https://github.com/tus/tus-js-client) so that you can test the protocol on your own machine.

## Clients
[Tus.io](http://tus.io/implementations.html) keeps a list of clients for a number of different platforms (Android, Java, JS, iOS etc). tusdotnet should work with all of them as long as they support version 1.0.0 of the protocol.

## Custom store
tusdotnet currently ships with a single store, the `TusDiskStore`, which saves files in a directory on disk. 
You can implement your own store by implementing the following interfaces:
* `ITusStore` - Support for the core protocol
* `ITusCreationStore` - Support for the Creation extension

Optionally the store can also implement the following interfaces:
* `ITusReadableStore` - Support for reading files from the store (e.g. for downloads or processing).

## Roadmap
* Next release:
  * Add support for file metadata
  * Add support for x-http-override (to support old clients)
* Future releases:
  *	Add support for Upload-Defer-Length
  * Add support for more extensions: Expiration, Checksum, Termination and Concatenation 
  * Add support for file tracking so that we can return 410 instead of 404 for abandoned files
  * Figure out a nice way of normalizing downloads with third party stores.

## License
This project is licensed under the MIT license, see [LICENSE](LICENSE).