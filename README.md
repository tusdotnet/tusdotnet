# tusdotnet

.NET server implementation of the Tus protocol for resumable file uploads. Read more at http://tus.io

From tus.io:
>Our aim is to solve the problem of unreliable file uploads once and for all. tus is a new open protocol for resumable uploads built on HTTP. It offers simple, cheap and reusable stacks for clients and servers. It supports any language, any platform and any network.

tusdotnet is a .NET server implementation of the Tus protocol that runs on both .NET 4.x and .NET Core!

Comments, ideas, questions and PRs are welcome :)

## Features

* Runs on both ASP.NET 4.x (using OWIN) and ASP.NET Core
* Full support for tus 1.0.0 including all major extensions (checksum, concatenation, creation, upload-defer-length, expiration and termination)
* Fast and reliable
* Easy to configure
* Customizable data storage
* MIT licensed

## Install

[![NuGet](https://img.shields.io/nuget/v/tusdotnet.svg)](https://www.nuget.org/packages/tusdotnet)

``PM> Install-Package tusdotnet``

## Configure

Create your Startup class as you would normally do. Add a using statement for `tusdotnet` and run `UseTus` on the app builder. You might also want to [configure IIS](https://github.com/smatsson/tusdotnet/wiki/Configure-IIS) and/or [configure CORS](https://github.com/smatsson/tusdotnet/wiki/Cross-domain-requests-(CORS)).

```csharp
app.UseTus(context => new DefaultTusConfiguration
{
	// c:\tusfiles is where to store files
	Store = new TusDiskStore(@"C:\tusfiles\"),
	// On what url should we listen for uploads?
	UrlPath = "/files",
	OnUploadCompleteAsync = async (fileId, store, cancellationToken) =>
	{
		var file = await ((ITusReadableStore)store).GetFileAsync(fileId, cancellationToken);
		await DoSomeProcessing(file);
	}
});
```
 
If you just want to play around with the protocol, clone the repo and run one of the test apps (one for OWIN and one for .NET Core). They each launch a small site running tusdotnet and the [official JS client](https://github.com/tus/tus-js-client) so that you can test the protocol on your own machine.

## Clients
[Tus.io](http://tus.io/implementations.html) keeps a list of clients for a number of different platforms (Android, Java, JS, iOS etc). tusdotnet should work with all of them as long as they support version 1.0.0 of the protocol.

## License
This project is licensed under the MIT license, see [LICENSE](LICENSE).

## Want to know more?
Check out the [wiki](https://github.com/smatsson/tusdotnet/wiki) or create an [issue](https://github.com/smatsson/tusdotnet/issues). :) 