
# tusdotnet
.NET server implementation of the Tus protocol for resumable file uploads. Read more at http://tus.io

## What?
From tus.io:
>tus is a new open protocol for resumable uploads built on HTTP. It offers simple, cheap and reusable stacks for clients and servers. It supports any language, any platform and any network.

tusdotnet is a .NET server implementation of the Tus protocol. It is written as a OWIN middleware for easy usage.

Currently in beta phase. The basic work flow of creating a file, uploading it and resuming the upload if the connection fails has been tested using the official JS client lib and is working.
Comments, ideas, questions and PRs are welcome :)

## Features
* Supports Tus 1.0.0 core protocol and the Creation extension
* Easy to use OWIN middleware
* Fast and reliable
* MIT licensed

## Installation

Clone this repository and compile using Visual Studio 2015. Include `tusdotnet.dll` in your project (or just include the source code).

## How to use? 

Setup OWIN as you would normally do. Add a using statement for `tusdotnet` and run UseTus on the IAppBuilder.

```csharp
// Each request will have its own instance of the configuration
app.UseTus(() => new DefaultTusConfiguration
			{
            	// c:\temp is where to store files
				Store = new TusDiskStore(@"C:\temp\"),
                // On what url should we listen for uploads?
				UrlPath = "/files"
			});
  ```

## Custom store
tusdotnet currently ships with a single store, the `TusDiskStore`, which saves files in a directory on disk. 
You can implement your own store by implementing the following interfaces:
* `ITusStore` - Support for the core protocol
* `ITusCreationStore` - Support for the Creation extension

## Project structure

* Source\tusdotnet contains the actual implementation 
* Source\OwinTestApp contains a small test app to test the implementation (solution also includes tusdotnet)

## TODO
* ~~Complete the implementation of the core protocol :)~~ 
* Implement extensions:
  * ~~Creation~~
  * Expiration
  * Checksum
  * Termination
  * Concatenation 
* Add support for http overrides (to support older browsers)
* Add support for file tracking so that we can return 410 instead of 404 for abandoned files
* Add support for file metadata 
* Add support for Upload-Defer-Length
* Write tests

## License
This project is licensed under the MIT license, see [LICENSE](LICENSE).
