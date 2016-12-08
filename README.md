# tusdotnet
.NET server implementation of the Tus protocol for resumable file uploads. Read more at http://tus.io

## What?
From tus.io:
>Our aim is to solve the problem of unreliable file uploads once and for all. tus is a new open protocol for resumable uploads built on HTTP. It offers simple, cheap and reusable stacks for clients and servers. It supports any language, any platform and any network.

tusdotnet is a .NET server implementation of the Tus protocol. It is written as a OWIN middleware for easy usage.

Comments, ideas, questions and PRs are welcome :)

## Features
* Supports Tus 1.0.0 core protocol and the Creation extension
* Easy to use OWIN middleware
* Fast and reliable
* 99% test coverage
* MIT licensed

## How to use

### Install

[![NuGet](https://img.shields.io/nuget/v/tusdotnet.svg)](https://www.nuget.org/packages/tusdotnet)

``PM> Install-Package tusdotnet``

### Setup

Setup OWIN as you would normally do. Add a using statement for `tusdotnet` and run `UseTus` on the IAppBuilder.

```csharp
app.UseTus(request => new DefaultTusConfiguration
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
```
 
If you just want to play around with the protocol, clone the repo and run the OwinTestApp. It launches a small site running tusdotnet and the [official JS client](https://github.com/tus/tus-js-client) so that you can test the protocol on your own machine.

## Clients
[Tus.io](http://tus.io/implementations.html) keeps a list of clients for a number of different platforms (Android, Java, JS, iOS etc). tusdotnet should work with all of them as long as they support version 1.0.0 of the protocol.

## License
This project is licensed under the MIT license, see [LICENSE](LICENSE).

## Want to know more?
Check out the [wiki](https://github.com/smatsson/tusdotnet/wiki) or create an [issue](https://github.com/smatsson/tusdotnet/issues). :) 