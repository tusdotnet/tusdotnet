
# tusdotnet
.NET server implementation of the Tus protocol for resumable file uploads. Read more at http://tus.io


## Intro
tusdotnet is being actively developed and is currently in early alpha, meaning it is not yet suitable for production. 

## Installation

Clone this repository and compile using Visual Studio 2015. Include `tusdotnet.dll` in your project (or just include the source code).

## How to use? 

Setup Owin as you would normally do. Add a using statement for `tusdotnet` and run UseTus on the IAppBuilder.

```csharp
app.UseTus(new DefaultTusConfiguration
			{
            	// c:\temp is where to store files
				Store = new TusDiskStore(@"C:\temp\"),
                // On what url should tus be running?
				UrlPath = "/files"
			});
  ```

## Custom store
tusdotnet currently ships with a single store, the `TusDiskStore`, which saves files in a directory on disk. You can implement your own store by implementing the `ITusStore` interface.

## Project structure

* Source\tusdotnet contains the actual implementation 
* Source\OwinTestApp contains a small test app to test the implementation (solution also includes tusdotnet)

## TODO
* Complete the implementation of the core protocol :) 
* Implement extensions:
  * Creation
  * Expiration
  * Checksum
  * Termination
  * Concatenation 
* Add support for http overrides (to support older browsers)
* Add support for file tracking so that we can return 410 instead of 404 for abandoned files.
* Write tests

## License
This project is licensed under the MIT license, see [LICENSE](LICENSE).
