# tusdotnet

[![NuGet](https://img.shields.io/nuget/v/tusdotnet.svg?color=blue&style=popout-square)](https://www.nuget.org/packages/tusdotnet) [![NuGet](https://img.shields.io/nuget/dt/tusdotnet.svg?color=blue&style=popout-square)](https://www.nuget.org/packages/tusdotnet) [![codecov](https://img.shields.io/codecov/c/github/tusdotnet/tusdotnet.svg?color=blue&style=popout-square)](https://codecov.io/gh/tusdotnet/tusdotnet)

>"Our aim is to solve the problem of unreliable file uploads once and for all. tus is a new open protocol for resumable uploads built on HTTP. It offers simple, cheap and reusable stacks for clients and servers. It supports any language, any platform and any network." - https://tus.io

tusdotnet is a .NET server implementation of the tus.io protocol that runs on both .NET 4.x, .NET Core, .NET6 and later.

Comments, ideas, questions and PRs are welcome!

## Features

* Runs on OWIN and ASP.NET Core (on both .NET Framework, .NET Core, .NET6 and later)
* Full support for tus 1.0.0 including all major extensions (checksum, checksum-trailers, concatenation, creation, creation-with-upload, upload-defer-length, expiration and termination)
* Fast and reliable
* Easy to configure
* Customizable data storage
* MIT licensed

## Install

Visual Studio

``PM> Install-Package tusdotnet``

.NET CLI

``> dotnet add package tusdotnet``

## Configure

On .NET6 and later:
```csharp

using tusdotnet;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapTus("/files", async httpContext => new()
{
    // This method is called on each request so different configurations can be returned per user, domain, path etc.
    // Return null to disable tusdotnet for the current request.

    // Where to store data?
    Store = new tusdotnet.Stores.TusDiskStore(@"C:\tusfiles\"),
    Events = new()
    {
        // What to do when file is completely uploaded?
        OnFileCompleteAsync = async eventContext =>
        {
            tusdotnet.Interfaces.ITusFile file = await eventContext.GetFileAsync();
            Dictionary<string, tusdotnet.Models.Metadata> metadata = await file.GetMetadataAsync(eventContext.CancellationToken);
            using Stream content = await file.GetContentAsync(eventContext.CancellationToken);

            await DoSomeProcessing(content, metadata);
        }
    }
});

```

 Depending on your infrastructure you might also need to [configure Kestrel](https://github.com/tusdotnet/tusdotnet/wiki/Configure-Kestrel), [IIS](https://github.com/tusdotnet/tusdotnet/wiki/Configure-IIS) or [other reverse proxies](https://github.com/tusdotnet/tusdotnet/wiki/Configure-other-reverse-proxies). 
 
 More options and events are available on the [wiki](https://github.com/tusdotnet/tusdotnet/wiki/Configuration).

<details>
<summary><h3>On older frameworks, use the tusdotnet middelware</h3></summary>

Create your Startup class as you would normally do. Add a using statement for `tusdotnet` and run `UseTus` on the app builder. Depending on your infrastructure you might also need to [configure Kestrel](https://github.com/tusdotnet/tusdotnet/wiki/Configure-Kestrel), [IIS](https://github.com/tusdotnet/tusdotnet/wiki/Configure-IIS) or [other reverse proxies](https://github.com/tusdotnet/tusdotnet/wiki/Configure-other-reverse-proxies).  More options and events are available on the [wiki](https://github.com/tusdotnet/tusdotnet/wiki/Configuration).

```csharp

app.UseTus(httpContext => new DefaultTusConfiguration
{
    // This method is called on each request so different configurations can be returned per user, domain, path etc.
    // Return null to disable tusdotnet for the current request.

    // c:\tusfiles is where to store files
    Store = new TusDiskStore(@"C:\tusfiles\"),
    // On what url should we listen for uploads?
    UrlPath = "/files",
    Events = new Events
    {
        OnFileCompleteAsync = async eventContext =>
        {
            ITusFile file = await eventContext.GetFileAsync();
            Dictionary<string, Metadata> metadata = await file.GetMetadataAsync(eventContext.CancellationToken);
            using Stream content = await file.GetContentAsync(eventContext.CancellationToken);

            await DoSomeProcessing(content, metadata);
        }
    }
});

```
</details>

## Test sites

If you just want to play around with tusdotnet/the tus protocol, clone the repo and run one of the test sites. They each launch a small site running tusdotnet and the [official JS client](https://github.com/tus/tus-js-client) so that you can test the protocol on your own machine. 

Test sites are available for:

* ASP.NET Core 6 (.NET 6.0)
* ASP.NET Core 3.1 (.NET Core 3.1)
* ASP.NET Core 3.0 (.NET Core 3.0)
* ASP.NET Core 2.2 (.NET Core 2.2)
* ASP.NET Core 2.2 (.NET Framework 4.6.2)
* ASP.NET Core 2.1 (.NET Core 2.1)
* OWIN (.NET Framework 4.5.2)

## Clients

[tus.io](http://tus.io/implementations.html) keeps a list of clients for a number of different platforms (Android, Java, JS, iOS etc). tusdotnet should work with all of them as long as they support version 1.0.0 of the protocol.

## License

This project is licensed under the MIT license, see [LICENSE](LICENSE).

## Want to know more?

Check out the [wiki](https://github.com/tusdotnet/tusdotnet/wiki) or create an [issue](https://github.com/tusdotnet/tusdotnet/issues)
