using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Owin;
using tusdotnet.Constants;
using tusdotnet.Interfaces;

namespace tusdotnet
{
	// TODO: Code cleanup
	/* Merge different places where error responses are built
	 * Merge response building
	 **/
	internal class TusMiddleware : OwinMiddleware
	{
		private readonly ITusConfiguration _config;
		private static readonly string[] SupportedMethods = { "post", "head", "patch", "options" };
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
				case "post":
					return HandlePostRequest(context);
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

		private async Task HandlePostRequest(IOwinContext context)
		{
			// TODO: Add support for metadata if the store supports it
			/*
			 * The Client MUST send a POST request against a known upload creation URL to request a new upload resource. 
			 * The request MUST include one of the following headers:
			 * a) Upload-Length to indicate the size of an entire upload in bytes.
			 * b) Upload-Defer-Length: 1 if upload size is not known at the time. 
			 * Once it is known the Client MUST set the Upload-Length header in the next PATCH request. 
			 * Once set the length MUST NOT be changed. As long as the length of the upload is not known, t
			 * he Server MUST set Upload-Defer-Length: 1 in all responses to HEAD requests.
			 * If the Server supports deferring length, it MUST add creation-defer-length to the Tus-Extension header.
			 * The Client MAY supply the Upload-Metadata header to add additional metadata to the upload creation request. 
			 * The Server MAY decide to ignore or use this information to further process the request or to reject it. 
			 * If an upload contains additional metadata, responses to HEAD requests MUST include the Upload-Metadata 
			 * header and its value as specified by the Client during the creation.
			 * If the length of the upload exceeds the maximum, which MAY be specified using the Tus-Max-Size header, 
			 * the Server MUST respond with the 413 Request Entity Too Large status.
			 * The Server MUST acknowledge a successful upload creation with the 201 Created status. 
			 * The Server MUST set the Location header to the URL of the created resource. This URL MAY be absolute or relative.
			 * The Client MUST perform the actual upload using the core protocol.
			 * */

			var tusCreationStore = _config.Store as ITusCreationStore;
			if (tusCreationStore == null)
			{
				await Next.Invoke(context);
				return;
			}

			// Kontrollera att Upload-Length eller upload-defer-length finns.
			// Om upload-defer-length så måste värdet vara 1.
			// Returnera 201 Created om allt gick bra.
			// Sätt location header (relative) till den nya filen, typ /files/asdf

			if (!context.Request.Headers.ContainsKey(HeaderConstants.UploadLength) &&
				!context.Request.Headers.ContainsKey(HeaderConstants.UploadDeferLength))
			{
				context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
				context.Response.ContentType = "text/plain";
				await
					context.Response.WriteAsync($"Missing {HeaderConstants.UploadLength} or {HeaderConstants.UploadDeferLength} header");
				return;
			}

			// TODO: Solve this in a nicer way
			long uploadLength = -1;

			if (context.Request.Headers.ContainsKey(HeaderConstants.UploadLength) &&
				!long.TryParse(context.Request.Headers[HeaderConstants.UploadLength], out uploadLength))
			{
				context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
				context.Response.ContentType = "text/plain";
				await context.Response.WriteAsync($"Could not parse {HeaderConstants.UploadLength}");
				return;
			}

			if (uploadLength == -1)
			{
				var uploadDeferLength = context.Request.Headers.ContainsKey(HeaderConstants.UploadDeferLength)
				? context.Request.Headers[HeaderConstants.UploadDeferLength]
				: null;

				if (string.IsNullOrWhiteSpace(uploadDeferLength) || uploadDeferLength != "1")
				{
					context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
					context.Response.ContentType = "text/plain";
					await context.Response.WriteAsync($"{HeaderConstants.UploadDeferLength} must equal \"1\"");
					return;
				}
			}


			string fileName;

			if (uploadLength == -1)
			{
				fileName = await tusCreationStore.CreateFileAsync(null);
			}
			else
			{
				fileName = await tusCreationStore.CreateFileAsync(uploadLength);
			}

			context.Response.Headers[HeaderConstants.TusResumable] = HeaderConstants.TusResumableValue;
			context.Response.Headers[HeaderConstants.Location] = _config.UrlPath.TrimEnd('/') + "/" + fileName;
			context.Response.StatusCode = (int)HttpStatusCode.Created;
		}

		private Task HandleOptionsRequest(IOwinContext context)
		{
			/*
			 * An OPTIONS request MAY be used to gather information about the Server’s current configuration. 
			 * A successful response indicated by the 204 No Content status MUST contain the Tus-Version header. 
			 * It MAY include the Tus-Extension and Tus-Max-Size headers.
			 * The Client SHOULD NOT include the Tus-Resumable header in the request and the Server MUST discard it.
			 * */

			context.Response.Headers[HeaderConstants.TusResumable] = HeaderConstants.TusResumableValue;
			context.Response.Headers[HeaderConstants.TusVersion] = HeaderConstants.TusResumableValue;

			var extensions = DetectExtensions();
			if (extensions.Any())
			{
				context.Response.Headers[HeaderConstants.TusExtension] = string.Join(",", extensions);
			}

			context.Response.StatusCode = (int)HttpStatusCode.NoContent;
			return Task.FromResult(true);
		}

		private async Task HandlePatchRequest(IOwinContext context)
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


			// TODO: Add concurrency locks

			var fileName = GetFileName(context.Request);

			var exists = await _config.Store.FileExistAsync(fileName);
			if (!exists)
			{
				context.Response.StatusCode = (int)HttpStatusCode.NotFound;
				context.Response.Headers[HeaderConstants.CacheControl] = HeaderConstants.NoStore;
				return;
			}

			if (!context.Request.ContentType.Equals("application/offset+octet-stream", StringComparison.InvariantCultureIgnoreCase))
			{
				context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
				context.Response.ContentType = "text/plain";
				await context
					.Response
					.WriteAsync($"Content-Type {context.Request.ContentType} is invalid. Must be application/offset+octet-stream");
				return;
			}

			if (!context.Request.Headers.ContainsKey(HeaderConstants.UploadOffset))
			{
				context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
				context.Response.ContentType = "text/plain";
				await context.Response.WriteAsync($"Missing {HeaderConstants.UploadOffset} header");
				return;
			}

			long requestOffset;

			if (!long.TryParse(context.Request.Headers[HeaderConstants.UploadOffset], out requestOffset))
			{
				context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
				context.Response.ContentType = "text/plain";
				await context.Response.WriteAsync($"Could not parse {HeaderConstants.UploadOffset} header");
				return;
			}

			var fileOffset = await _config.Store.GetUploadOffsetAsync(fileName);

			if (requestOffset != fileOffset)
			{
				context.Response.StatusCode = (int)HttpStatusCode.Conflict;
				context.Response.ContentType = "text/plain";
				await
					context.Response.WriteAsync($"Offset does not match file. File offset: {fileOffset}. Request offset: {requestOffset}");
				return;
			}

			long bytesWritten = 0;
			try
			{
				int bytesRead;
				do
				{
					var buffer = new byte[ByteChunkSize];
					bytesRead = context.Request.Body.Read(buffer, 0, ByteChunkSize);
					await _config.Store.AppendDataAsync(fileName, buffer.Take(bytesRead).ToArray());
					bytesWritten += bytesRead;
				} while (bytesRead != 0);
			}
			catch (Exception exc)
			{
				Console.WriteLine(exc.Message);
			}

			context.Response.StatusCode = (int)HttpStatusCode.NoContent;
			context.Response.Headers[HeaderConstants.TusResumable] = HeaderConstants.TusResumableValue;
			context.Response.Headers[HeaderConstants.UploadOffset] = (fileOffset + bytesWritten).ToString();
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
				context.Response.Headers[HeaderConstants.UploadLength] = uploadLength.Value.ToString();
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

		private List<string> DetectExtensions()
		{
			var extensions = new List<string>();
			if (_config.Store is ITusCreationStore)
			{
				extensions.Add(ExtensionConstants.Creation);
			}

			return extensions;
		}
	}
}
