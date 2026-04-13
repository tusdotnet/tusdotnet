# tusdotnet

[![NuGet](https://img.shields.io/nuget/v/tusdotnet.svg?color=blue&style=popout-square)](https://www.nuget.org/packages/tusdotnet) [![NuGet](https://img.shields.io/nuget/dt/tusdotnet.svg?color=blue&style=popout-square)](https://www.nuget.org/packages/tusdotnet) [![codecov](https://img.shields.io/codecov/c/github/tusdotnet/tusdotnet.svg?color=blue&style=popout-square)](https://codecov.io/gh/tusdotnet/tusdotnet)

>"Our aim is to solve the problem of unreliable file uploads once and for all. tus is a new open protocol for resumable uploads built on HTTP. It offers simple, cheap and reusable stacks for clients and servers. It supports any language, any platform and any network." - https://tus.io

tusdotnet is a .NET server implementation of the tus.io protocol that runs on .NET Framework, .NET Standard, .NET 6 and later.

## Features

* Runs on .NET Framework, .NET Standard 1.3+ and .NET 6+ using OWIN or ASP.NET Core
* Full support for tus 1.0.0 including all major extensions (checksum, checksum-trailers, concatenation, creation, creation-with-upload, upload-defer-length, expiration and termination)
* Experimental support for IETF's [Resumable Uploads For Http](https://datatracker.ietf.org/doc/draft-ietf-httpbis-resumable-upload/) (see branch [POC/tus2](https://github.com/tusdotnet/tusdotnet/tree/POC/tus2))
* Fast and reliable
* Easy to configure
* Customizable data storage with built-in disk storage (`TusDiskStore`) and community stores for [Azure Blob Storage](https://github.com/giometrix/Xtensible.TusDotNet.Azure) and [S3-compatible storage](https://github.com/bechtleav360/tusdotnet.Storage.S3) (AWS S3, MinIO, Cloudflare R2, etc.)
* MIT licensed

## Getting started

```
dotnet add package tusdotnet
```

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
        // What to do when the file is completely uploaded?
        OnFileCompleteAsync = async eventContext =>
        {
            var file = await eventContext.GetFileAsync();

            await QueueForProcessing(file);
        }
    }
});
```

Depending on your infrastructure you might also need to [configure Kestrel](https://github.com/tusdotnet/tusdotnet/wiki/Configure-Kestrel), [IIS](https://github.com/tusdotnet/tusdotnet/wiki/Configure-IIS) or [other reverse proxies](https://github.com/tusdotnet/tusdotnet/wiki/Configure-other-reverse-proxies).

More options, events and [middleware usage](https://github.com/tusdotnet/tusdotnet/wiki/Configure-tusdotnet#endpoint-routing-or-middleware) are available on the [wiki](https://github.com/tusdotnet/tusdotnet/wiki/Configuration).

## Try it out

Clone the repo and run one of the [test sites](https://github.com/tusdotnet/tusdotnet/tree/master/Source/TestSites). They each launch a small site running tusdotnet and the [official JS client](https://github.com/tus/tus-js-client) so that you can test the protocol on your own machine.

## Clients

[tus.io](https://tus.io/implementations.html) keeps a list of clients for a number of different platforms (Android, Java, JS, iOS etc). tusdotnet should work with all of them as long as they support version 1.0.0 of the protocol.

## License

This project is licensed under the MIT license, see [LICENSE](LICENSE).
