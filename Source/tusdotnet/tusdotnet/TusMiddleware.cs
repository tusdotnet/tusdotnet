using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Owin;
using tusdotnet.Interfaces;

namespace tusdotnet
{
	internal class TusMiddleware : OwinMiddleware
	{
		private readonly ITusConfiguration _config;
		private static readonly string[] SupportedMethods = { "head", "patch", "options" };
		// Size for each read, should be configurable.
		// The lower value, the less is needed to be re-uploaded but this comes with a performance hit.
		private const int ByteChunkSize = 51250;

		public TusMiddleware(OwinMiddleware next, ITusConfiguration config) : base(next)
		{
			_config = config;
			// TODO: Verify configuration
		}

		public override Task Invoke(IOwinContext context)
		{
			if (!ShouldHandleRequest(context.Request))
			{
				return Next.Invoke(context);
			}

			var tusResumable = context.Request.Headers[HeaderConstants.TusResumable];

			if (tusResumable != HeaderConstants.TusResumableValue)
			{
				context.Response.Headers[HeaderConstants.TusResumable] = HeaderConstants.TusResumableValue;
				context.Response.Headers[HeaderConstants.TusVersion] = HeaderConstants.TusResumableValue;
				context.Response.StatusCode = (int)HttpStatusCode.PreconditionFailed;
				context.Response.ContentType = "text/plain";
				return
					context.Response.WriteAsync(
						$"Tus version {tusResumable} is not supported. Supported versions: {HeaderConstants.TusResumableValue}");
			}

			// TODO: Error handling.
			switch (context.Request.Method.ToLower())
			{
				case "head":
					return HandleHeadRequest(context);
				case "patch":
					return HandlePatchRequest(context);
				case "options":
					return HandleOptionsRequest(context);
				default:
					throw new NotImplementedException($"HTTP method {context.Request.Method.ToLower()} is not implemented.");
			}
		}

		private static Task HandleOptionsRequest(IOwinContext context)
		{
			/*
			 * An OPTIONS request MAY be used to gather information about the Server’s current configuration. 
			 * A successful response indicated by the 204 No Content status MUST contain the Tus-Version header. 
			 * It MAY include the Tus-Extension and Tus-Max-Size headers.
			 * The Client SHOULD NOT include the Tus-Resumable header in the request and the Server MUST discard it.
			 * */

			// TODO: Add extensions once implemented.
			context.Response.Headers[HeaderConstants.TusResumable] = HeaderConstants.TusResumableValue;
			context.Response.Headers[HeaderConstants.TusVersion] = HeaderConstants.TusResumableValue;
			context.Response.StatusCode = (int)HttpStatusCode.NoContent;
			return Task.FromResult(true);
		}

		private Task HandlePatchRequest(IOwinContext context)
		{
			/*
			 * The Server SHOULD accept PATCH requests against any upload URL and apply the bytes 
			 * contained in the message at the given offset specified by the Upload-Offset header. 
			 * All PATCH requests MUST use Content-Type: application/offset+octet-stream.
			 * The Upload-Offset header’s value MUST be equal to the current offset of the resource. 
			 * In order to achieve parallel upload the Concatenation extension MAY be used. 
			 * If the offsets do not match, the Server MUST respond with the 409 Conflict status without modifying the upload resource.
			 * The Client SHOULD send all the remaining bytes of an upload in a single PATCH request, 
			 * but MAY also use multiple small requests successively for scenarios where 
			 * this is desirable, for example, if the Checksum extension is used.
			 * The Server MUST acknowledge successful PATCH requests with the 204 No Content status. 
			 * It MUST include the Upload-Offset header containing the new offset. 
			 * The new offset MUST be the sum of the offset before the PATCH request and the number of bytes received and 
			 * processed or stored during the current PATCH request.
			 * Both, Client and Server, SHOULD attempt to detect and handle network errors predictably. 
			 * They MAY do so by checking for read/write socket errors, as well as setting read/write timeouts. 
			 * A timeout SHOULD be handled by closing the underlying connection.
			 * The Server SHOULD always attempt to store as much of the received data as possible.
			 * */

			try
			{
				int bytesRead;
				do
				{
					var buffer = new byte[ByteChunkSize];
					bytesRead = context.Request.Body.Read(buffer, 0, ByteChunkSize);
					// TODO: Get file name from URL.
					_config.Store.AppendDataAsync(@"C:\test.txt", buffer.Take(bytesRead).ToArray());

				} while (bytesRead != 0);

				context.Response.ContentType = "text/plain";
				return context.Response.WriteAsync("File uploaded");

			}
			catch (Exception exc)
			{
				Console.WriteLine(exc.Message);
			}

			throw new NotImplementedException();
		}

		private async Task HandleHeadRequest(IOwinContext context)
		{
			/*
			 * The Server MUST always include the Upload-Offset header in the response for a HEAD request, 
			 * even if the offset is 0, or the upload is already considered completed. If the size of the upload is known, 
			 * the Server MUST include the Upload-Length header in the response. 
			 * If the resource is not found, the Server SHOULD return either the 404 Not Found, 410 Gone or 403 Forbidden 
			 * status without the Upload-Offset header.
			 * The Server MUST prevent the client and/or proxies from caching the response by adding the 
			 * Cache-Control: no-store header to the response.
			 * */

			var fileName = GetFileName(context.Request);

			var exists = await _config.Store.FileExistAsync(fileName);
			if (!exists)
			{
				context.Response.StatusCode = (int)HttpStatusCode.NotFound;
				context.Response.Headers[HeaderConstants.CacheControl] = HeaderConstants.NoStore;
				return;
			}

			var uploadLength = await _config.Store.GetUploadLengthAsync(fileName);
			if (uploadLength != null)
			{
				context.Response.Headers[HeaderConstants.UploadOffset] = uploadLength.Value.ToString();
			}

			var uploadOffset = await _config.Store.GetUploadOffsetAsync(fileName);
			context.Response.Headers[HeaderConstants.TusResumable] = HeaderConstants.TusResumableValue;
			context.Response.Headers[HeaderConstants.UploadOffset] = uploadOffset.ToString();
			context.Response.Headers[HeaderConstants.CacheControl] = HeaderConstants.NoStore;
		}

		private bool ShouldHandleRequest(IOwinRequest request)
		{
			return request.Uri.LocalPath.StartsWith(_config.UrlPath, StringComparison.InvariantCultureIgnoreCase)
				   && SupportedMethods.Contains(request.Method.ToLower())
				   && request.Headers.ContainsKey(HeaderConstants.TusResumable);
		}

		private string GetFileName(IOwinRequest request)
		{
			var startIndex = request
								 .Uri
								 .LocalPath
								 .IndexOf(_config.UrlPath, StringComparison.InvariantCultureIgnoreCase) + _config.UrlPath.Length;
			return request
				.Uri
				.LocalPath
				.Substring(startIndex)
				.Trim('/');
		}
	}
}
