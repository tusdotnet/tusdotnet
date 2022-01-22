# tusdotnet

[![NuGet](https://img.shields.io/nuget/v/tusdotnet.svg?color=blue&style=popout-square)](https://www.nuget.org/packages/tusdotnet) [![NuGet](https://img.shields.io/nuget/dt/tusdotnet.svg?color=blue&style=popout-square)](https://www.nuget.org/packages/tusdotnet) [![codecov](https://img.shields.io/codecov/c/github/tusdotnet/tusdotnet.svg?color=blue&style=popout-square)](https://codecov.io/gh/tusdotnet/tusdotnet)

>"Our aim is to solve the problem of unreliable file uploads once and for all. tus is a new open protocol for resumable uploads built on HTTP. It offers simple, cheap and reusable stacks for clients and servers. It supports any language, any platform and any network." - https://tus.io

tusdotnet is a .NET server implementation of the tus.io protocol that runs on both .NET 4.x and .NET Core!

This branch is intended for the new [tus2 protocol](https://github.com/tus/tus-v2). For tus 1.x, see the main branch: https://github.com/tusdotnet/tusdotnet/

Please note that this is a POC/experimental implementation and breaking changes will happen.

## Install

Clone this branch and include it in your project. All classes related to tus2 are found in the `tusdotnet.tus2` namespace. Files are found in `Source/tusdotnet/tus2`.

## Configure (simple)

In Startup.cs add the following:

```csharp

public void ConfigureServices(IServiceCollection services)
{
    var tus2Configuration = new Tus2Options();
    Configuration.Bind(tus2Configuration);

    services.AddTus2(options =>
    {
        // Shorthand for adding a scoped implementation of Tus2DiskStorage to the DI container
        options.AddDiskStorage(tus2Configuration.FolderDiskPath);

        // Adds MyTusHandler as transient
        options.AddHandler<MyTusHandler>();
    });
}

public void Configure(IApplicationBuilder app)
{
    app.UseRouting();
    app.UseEndpoints(endpoints =>
    {
        endpoints.MapTus2<MyTusHandler>("/files-tus-2");
    });
}

```

Define a class called `MyTusHandler` that inherits from `tusdotnet.Tus2.TusHandler` and override the methods you would like to handle. The `TusHandler` base class will handle communication with storage so remember to call the base implementation in your override.

```csharp
public class MyTusHandler : TusHandler
{
    private readonly ILogger _logger;

    public MyTusHandler(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(nameof(MyTusHandler));
    }

    public override async Task<CreateFileProcedureResponse> OnCreateFile(CreateFileContext createFileContext)
    {
        _logger.LogInformation("Creating file {UploadToken}", Headers.UploadToken);

        var response = await base.OnCreateFile(createFileContext);

        _logger.LogInformation("File created? {Success}", response.Status == System.Net.HttpStatusCode.Created);

        return response;
    }

    public override async Task<UploadTransferProcedureResponse> OnWriteData(WriteDataContext writeDataContext)
    {
        _logger.LogInformation("Receiving upload, starting at {UploadOffset}", Headers.UploadOffset);
        
        var response = await base.OnWriteData(writeDataContext);

        _logger.LogInformation("Was success? {Success}", response.Status == System.Net.HttpStatusCode.Created);

        return response;
    }

    public override async Task<UploadRetrievingProcedureResponse> OnRetrieveOffset()
    {
        _logger.LogInformation("Retrieving offset for {UploadToken}", Headers.UploadToken);

        var response = await base.OnRetrieveOffset();

        _logger.LogInformation("Offset is {UploadOffset}", response.UploadOffset);

        return response;
    }

    public override async Task<UploadCancellationProcedureResponse> OnDelete()
    {
        _logger.LogInformation("Deleting file {UploadToken}", Headers.UploadToken);

        var response = await base.OnDelete();

        _logger.LogInformation("File deleted? {Deleted}", response.Status == System.Net.HttpStatusCode.NoContent);

        return response;
    }

    public override Task OnFileComplete()
    {
        _logger.LogInformation("File {UploadToken} is complete", Headers.UploadToken);

        return base.OnFileComplete();
    }
}
```

## Configure (more complex)

The tus2 implementation supports the concept of "named configurations". These are configured as below.

```csharp
services.AddTus2(options =>
{
    options.AddDiskStorage("MyProfile", tus2Configuration.FolderDiskPath);
    options.AddHandler<MyTusHandler>();
});

app.UseEndpoints(endpoints =>
{
    var filesTus2Config = new EndpointConfiguration("MyProfile")
    {
        AllowClientToDeleteFile = true
    };

    endpoints.MapTus2<MyTusHandler>("/files-tus-2", filesTus2Config);
});

```

## Configure storage (more complex)

tus2, just as tus1, supports multiple different storage implementations ("stores" in tus1).

```csharp
services.AddTus2(options =>
{
    // Default configuration
    // Equivalent of options.AddDiskStorage(tus2Configuration.FolderDiskPath);
    options.AddStorage(new Tus2DiskStore(new()
    {
        DiskPath = tus2Configuration.FolderDiskPath
    }));

    // Named configuration
    // Equivalent of options.AddDiskStorage(tus2Configuration.FolderDiskPath);
    options.AddStorage("MyProfile", new Tus2DiskStore(new()
    {
        DiskPath = tus2Configuration.FolderDiskPath
    }));
});

```

And supports creating the storage implementation on the fly based on the current http context.

```csharp

// Default configuration
options.AddStorage(httpContext => new Tus2DiskStore(new()
{
    DiskPath = System.IO.Path.Combine(tus2Configuration.FolderDiskPath, httpContext.User.Identity.Name)
}));

// Named configuration
options.AddStorage("MyProfile", httpContext => new Tus2DiskStore(new()
{
    DiskPath = System.IO.Path.Combine(tus2Configuration.FolderDiskPath, "MyProfile", httpContext.User.Identity.Name)
}));

```

## Configure ongoing upload manager (more complex)

In tus2 locks are not used. Instead all previous upload requests for a single `Upload-Token` must be terminated when a new request for the same `Upload-Token` is received. In tusdotnet this is handled by the `IOngoingUploadManager`. It can be configured in much the same way as the storage. By default, an `OngoingUploadManagerInMemory` instance will be used. If you run your setup in a cluster you will need to switch to either `OngoingUploadManagerDiskBased` and point it to a shared disk or implement your own.

```csharp
services.AddTus2(options =>
{
    // Default configuration
    options.AddDiskBasedUploadManager(@"C:\tusfiles");
    // OR
    options.AddUploadManager(new OngoingUploadManagerDiskBased(new() { SharedDiskPath = @"C:\tusfiles" }));

    // Named configuration
    options.AddDiskBasedUploadManager("MyProfile", @"C:\tusfiles");
    // OR
    options.AddUploadManager("MyProfile", new OngoingUploadManagerDiskBased(new() { SharedDiskPath = @"C:\tusfiles" }));
});

```

## How do I...? 

### Run the tus2 implementation in a cluster/on multiple machines?
Register the `OngoingUploadManagerDiskBased` in your DI and tusdotnet will automatically solve the new locking behavior. You can also implement your own implementation of `IOngoingUploadManager` and use that

### How do I access the storage outside my tus handler?

Default configurations that does not use the "create using the current http context" featured are registered directly in the DI container as scoped instances. Other configurations are accessed through `ITus2ConfigurationManager` which also supports getting the default implementation.

```csharp

services.AddTus2(options =>
{
    // Defaults
    options.AddStorage(async httpContext => new Tus2DiskStore(new()
    {
        DiskPath = System.IO.Path.Combine(tus2Configuration.FolderDiskPath, httpContext.User.Identity.Name)
    }));
    options.AddUploadManager(new OngoingUploadManagerDiskBased(new() { SharedDiskPath = System.IO.Path.GetTempPath() }));

    // Named
    options.AddDiskStorage("MyProfile", tus2Configuration.FolderDiskPath);
    options.AddDiskBasedUploadManager("MyUploadProfile", @"C:\tusfiles");
});

public class MyService
{
    private readonly ITus2ConfigurationManager _config;
    private readonly ITus2Storage _defaultStorage;
    private readonly IOngoingUploadManager _defaultUploadManager;

    // Note that ITus2Storage cannot be injected here as the default storage registered in the DI container uses the storage factory pattern.
    public MyService(
        ITus2ConfigurationManager config,
        IOngoingUploadManager defaultUploadManager)
    {
        _config = config;
        _defaultUploadManager = defaultUploadManager;
    }

    public async Task MyMethod()
    {
        var defaultStorage = await _config.GetDefaultStorage();
        var defaultStorage2 = await _config.GetDefaultStorage();
        var myProfileStorage = await _config.GetNamedStorage("MyProfile");

        var defaultUploadManager = await _config.GetDefaultUploadManager();
        var myUploadProfile = await _config.GetNamedUploadManager("MyUploadProfile");

        // True, the storage factory is only called once per scope.
        Assert.AreEqual(defaultStorage, defaultStorage2);

        // True
        Assert.AreEqual(defaultUploadManager, _defaultUploadManager);
    }
}
```


## Test sites

Test site only is available for ASP.NET Core 3.1 (.NET Core 3.1) as the tus2 implementation requires .NET classes only found in Core 3.1 and later.

## License

This project is licensed under the MIT license, see [LICENSE](LICENSE).

## Want to know more?

Discussion can be held in this issue: https://github.com/tusdotnet/tusdotnet/issues/164