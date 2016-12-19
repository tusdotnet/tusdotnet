using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Owin;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace tusdotnet
{
	internal class TusMiddleware : OwinMiddleware
	{
		private readonly Func<IOwinRequest, ITusConfiguration> _configFactory;
		private ITusConfiguration _config;
		

		public TusMiddleware(OwinMiddleware next, Func<IOwinRequest, ITusConfiguration> configFactory) : base(next)
		{
			_configFactory = configFactory;
		}

		public override Task Invoke(IOwinContext context)
		{
			_config = _configFactory(context.Request);
			ValidateConfig();

			if (!ShouldHandleRequest(context.Request))
			{
				return Next.Invoke(context);
			}

			var method = context.Request.GetMethod();

			var tusResumable = context.Request.Headers[HeaderConstants.TusResumable];

			if (method != "options" && tusResumable != HeaderConstants.TusResumableValue)
			{
				context.Response.Headers[HeaderConstants.TusResumable] = HeaderConstants.TusResumableValue;
				context.Response.Headers[HeaderConstants.TusVersion] = HeaderConstants.TusResumableValue;
				return RespondAsync(context,
					HttpStatusCode.PreconditionFailed,
					$"Tus version {tusResumable} is not supported. Supported versions: {HeaderConstants.TusResumableValue}");
			}

			switch (method)
			{
				case "post":
					return HandlePostRequest(context);
				case "head":
					return HandleHeadRequest(context);
				case "patch":
					return HandlePatchRequest(context);
				case "options":
					return HandleOptionsRequest(context);
				case "delete":
					return HandleDeleteRequest(context);
				default:
					throw new NotImplementedException($"HTTP method {method} is not implemented.");
			}
		}

		private async Task HandleDeleteRequest(IOwinContext context)
		{
			/* When receiving a DELETE request for an existing upload the Server SHOULD free associated resources and MUST 
			 * respond with the 204 No Content status confirming that the upload was terminated. 
			 * For all future requests to this URL the Server SHOULD respond with the 404 Not Found or 410 Gone status.
			 * */

			var store = _config.Store as ITusTerminationStore;
			if (store == null)
			{
				await Next.Invoke(context);
				return;
			}

			var fileId = GetFileName(context.Request);
			var fileLock = new FileLock(fileId);

			var hasLock = fileLock.Lock(context.Request.CallCancelled);
			if (!hasLock)
			{
				await
					RespondAsync(context, HttpStatusCode.Conflict,
						$"File {fileId} is currently being updated. Please try again later");
				return;
			}

			try
			{
				var exists = await _config.Store.FileExistAsync(fileId, context.Request.CallCancelled);
				if (!exists)
				{
					context.Response.StatusCode = (int)HttpStatusCode.NotFound;
					context.Response.Headers[HeaderConstants.TusResumable] = HeaderConstants.TusResumableValue;
					context.Response.Headers[HeaderConstants.CacheControl] = HeaderConstants.NoStore;
					return;
				}

				await store.DeleteFileAsync(fileId, context.Request.CallCancelled);
				context.Response.StatusCode = (int)HttpStatusCode.NoContent;
				context.Response.Headers[HeaderConstants.TusResumable] = HeaderConstants.TusResumableValue;
			}
			finally
			{
				fileLock.ReleaseIfHeld();
			}
		}

		private async Task HandlePostRequest(IOwinContext context)
		{
			// TODO: Add support for Defer-Upload-Length if the store supports it

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

			if (!context.Request.Headers.ContainsKey(HeaderConstants.UploadLength))
			{
				await RespondAsync(context, HttpStatusCode.BadRequest, $"Missing {HeaderConstants.UploadLength} header");
				return;
			}

			long uploadLength;
			if (!long.TryParse(context.Request.Headers[HeaderConstants.UploadLength], out uploadLength))
			{
				await RespondAsync(context, HttpStatusCode.BadRequest, $"Could not parse {HeaderConstants.UploadLength}");
				return;
			}

			if (uploadLength < 0)
			{
				await RespondAsync(context,
					HttpStatusCode.BadRequest,
					$"Header {HeaderConstants.UploadLength} must be a positive number");
				return;
			}

			if (_config.MaxAllowedUploadSizeInBytes.HasValue && uploadLength > _config.MaxAllowedUploadSizeInBytes.Value)
			{
				await RespondAsync(context,
					HttpStatusCode.RequestEntityTooLarge,
					$"Header {HeaderConstants.UploadLength} exceeds the server's max file size.");
				return;
			}

			if (!(await ValidateMetadataHeader(context)))
			{
				return;
			}

			var fileName = await tusCreationStore.CreateFileAsync(uploadLength, context.Request.Headers.Get(HeaderConstants.UploadMetadata), context.Request.CallCancelled);

			context.Response.Headers[HeaderConstants.TusResumable] = HeaderConstants.TusResumableValue;
			context.Response.Headers[HeaderConstants.Location] = _config.UrlPath.TrimEnd('/') + "/" + fileName;
			context.Response.StatusCode = (int)HttpStatusCode.Created;
		}

		private async Task<bool> ValidateMetadataHeader(IOwinContext context)
		{
			/* 
             * The Upload-Metadata request and response header MUST consist of one or more comma - separated key - value pairs.
             * The key and value MUST be separated by a space. The key MUST NOT contain spaces and commas and MUST NOT be empty.
             * The key SHOULD be ASCII encoded and the value MUST be Base64 encoded.All keys MUST be unique.
             * */

			if (context.Request.Headers.ContainsKey(HeaderConstants.UploadMetadata))
			{
				string metadata = context.Request.Headers.Get(HeaderConstants.UploadMetadata);
				if (string.IsNullOrEmpty(metadata))
				{
					await RespondAsync(context,
						HttpStatusCode.BadRequest,
						$"Header {HeaderConstants.UploadMetadata} must consist of one or more comma-separated key-value pairs");
					return false;
				}

				HashSet<string> keys = new HashSet<string>();
				var pairs = metadata.Split(',');
				foreach (var pair in pairs)
				{
					var pairParts = pair.Split(' ');
					if (pairParts.Count() != 2)
					{
						await RespondAsync(context,
							HttpStatusCode.BadRequest,
							$"Header {HeaderConstants.UploadMetadata}: The Upload-Metadata request and response header MUST consist of one or more comma - separated key - value pairs. The key and value MUST be separated by a space.The key MUST NOT contain spaces and commas and MUST NOT be empty. The key SHOULD be ASCII encoded and the value MUST be Base64 encoded.All keys MUST be unique.");
						return false;
					}

					var key = pairParts.First();
					if (string.IsNullOrEmpty(key))
					{
						await RespondAsync(context,
							HttpStatusCode.BadRequest,
							$"Header {HeaderConstants.UploadMetadata}: Key must not be empty");
						return false;
					}

					if (keys.Contains(key))
					{
						await RespondAsync(context,
							HttpStatusCode.BadRequest,
							$"Header {HeaderConstants.UploadMetadata}: Duplicate keys are not allowed");
						return false;
					}

					var value = pairParts.Skip(1).First();

					try
					{
						// ReSharper disable once ReturnValueOfPureMethodIsNotUsed
						Convert.FromBase64String(value);
					}
					catch (FormatException)
					{
						await RespondAsync(context,
							HttpStatusCode.BadRequest,
							$"Header {HeaderConstants.UploadMetadata}: Value for {key} is not properly encoded using base64");
						return false;
					}

					keys.Add(key);
				}
			}

			return true;
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

			if (_config.MaxAllowedUploadSizeInBytes.HasValue)
			{
				context.Response.Headers[HeaderConstants.TusMaxSize] = _config.MaxAllowedUploadSizeInBytes.Value.ToString();
			}

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

			var fileName = GetFileName(context.Request);
			var cancellationToken = context.Request.CallCancelled;
			var fileLock = new FileLock(fileName);


			var hasLock = fileLock.Lock(cancellationToken);

			if (!hasLock)
			{
				await
					RespondAsync(context, HttpStatusCode.Conflict,
						$"File {fileName} is currently being updated. Please try again later");
				return;
			}

			try
			{
				if (context.Request.ContentType == null ||
					!context.Request.ContentType.Equals("application/offset+octet-stream",
						StringComparison.InvariantCultureIgnoreCase))
				{
					await RespondAsync(context,
						HttpStatusCode.BadRequest,
						$"Content-Type {context.Request.ContentType} is invalid. Must be application/offset+octet-stream");
					return;
				}

				if (!context.Request.Headers.ContainsKey(HeaderConstants.UploadOffset))
				{
					await RespondAsync(context, HttpStatusCode.BadRequest, $"Missing {HeaderConstants.UploadOffset} header");
					return;
				}

				long requestOffset;

				if (!long.TryParse(context.Request.Headers[HeaderConstants.UploadOffset], out requestOffset))
				{
					await RespondAsync(context, HttpStatusCode.BadRequest, $"Could not parse {HeaderConstants.UploadOffset} header");
					return;
				}

				if (requestOffset < 0)
				{
					await RespondAsync(context, HttpStatusCode.BadRequest,
						$"Header {HeaderConstants.UploadOffset} must be a positive number");
					return;
				}

				var exists = await _config.Store.FileExistAsync(fileName, cancellationToken);
				if (!exists)
				{
					context.Response.StatusCode = (int)HttpStatusCode.NotFound;
					context.Response.Headers[HeaderConstants.TusResumable] = HeaderConstants.TusResumableValue;
					context.Response.Headers[HeaderConstants.CacheControl] = HeaderConstants.NoStore;
					return;
				}

				var fileOffset = await _config.Store.GetUploadOffsetAsync(fileName, cancellationToken);

				if (requestOffset != fileOffset)
				{
					await RespondAsync(context,
						HttpStatusCode.Conflict,
						$"Offset does not match file. File offset: {fileOffset}. Request offset: {requestOffset}");
					return;
				}

				var fileUploadLength = await _config.Store.GetUploadLengthAsync(fileName, cancellationToken);

				if (fileUploadLength != null && fileOffset == fileUploadLength.Value)
				{
					await RespondAsync(context, HttpStatusCode.BadRequest, "Upload is already complete.");
					return;
				}

				long bytesWritten;
				try
				{
					bytesWritten = await _config.Store.AppendDataAsync(fileName, context.Request.Body, cancellationToken);
				}
				catch (IOException ioException)
				{
					// Indicates that the client disconnected.
					if (!(ioException.InnerException is HttpListenerException))
					{
						throw;
					}

					// Client disconnected so no need to return a response.
					return;
				}
				catch (TusStoreException storeException)
				{
					await RespondAsync(context, HttpStatusCode.BadRequest, storeException.Message);
					throw;
				}

				context.Response.StatusCode = (int)HttpStatusCode.NoContent;
				context.Response.Headers[HeaderConstants.TusResumable] = HeaderConstants.TusResumableValue;
				context.Response.Headers[HeaderConstants.UploadOffset] = (fileOffset + bytesWritten).ToString();

				// Run OnUploadComplete if it has been provided.
				if (fileUploadLength != null
					&& (fileOffset + bytesWritten) == fileUploadLength.Value
					&& _config.OnUploadCompleteAsync != null)
				{
					await _config.OnUploadCompleteAsync(fileName, _config.Store, context.Request.CallCancelled);
				}
			}
			finally
			{
				fileLock.ReleaseIfHeld();
			}
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
			 * 
			 * If an upload contains additional metadata, responses to HEAD requests MUST include the Upload-Metadata header 
			 * and its value as specified by the Client during the creation.
			 * */

			var fileName = GetFileName(context.Request);
			var cancellationToken = context.Request.CallCancelled;

			var exists = await _config.Store.FileExistAsync(fileName, cancellationToken);
			if (!exists)
			{
				context.Response.StatusCode = (int)HttpStatusCode.NotFound;
				context.Response.Headers[HeaderConstants.CacheControl] = HeaderConstants.NoStore;
				context.Response.Headers[HeaderConstants.TusResumable] = HeaderConstants.TusResumableValue;
				return;
			}

			var uploadLength = await _config.Store.GetUploadLengthAsync(fileName, cancellationToken);
			if (uploadLength != null)
			{
				context.Response.Headers[HeaderConstants.UploadLength] = uploadLength.Value.ToString();
			}

			var tusCreationStore = _config.Store as ITusCreationStore;
			if (tusCreationStore != null)
			{
				var uploadMetadata = await tusCreationStore.GetUploadMetadataAsync(fileName, cancellationToken);
				if (!string.IsNullOrEmpty(uploadMetadata))
				{
					context.Response.Headers[HeaderConstants.UploadMetadata] = uploadMetadata;
				}
			}

			var uploadOffset = await _config.Store.GetUploadOffsetAsync(fileName, cancellationToken);
			context.Response.Headers[HeaderConstants.TusResumable] = HeaderConstants.TusResumableValue;
			context.Response.Headers[HeaderConstants.UploadOffset] = uploadOffset.ToString();
			context.Response.Headers[HeaderConstants.CacheControl] = HeaderConstants.NoStore;
		}

		private bool ShouldHandleRequest(IOwinRequest request)
		{
			var method = request.GetMethod();

			if (!request.Headers.ContainsKey(HeaderConstants.TusResumable) && method != "options")
			{
				return false;
			}

			switch (method)
			{
				case "post":
				case "options":
					return IsExactUrlMatch(request);
				case "head":
				case "patch":
				case "delete":
					return !IsExactUrlMatch(request) &&
						   request.Uri.LocalPath.StartsWith(_config.UrlPath, StringComparison.InvariantCultureIgnoreCase);
				default:
					return false;
			}
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

		private bool IsExactUrlMatch(IOwinRequest request)
		{
			return request.Uri.LocalPath.TrimEnd('/') == _config.UrlPath.TrimEnd('/');
		}

		private List<string> DetectExtensions()
		{
			var extensions = new List<string>();
			if (_config.Store is ITusCreationStore)
			{
				extensions.Add(ExtensionConstants.Creation);
			}

			if (_config.Store is ITusTerminationStore)
			{
				extensions.Add(ExtensionConstants.Termination);
			}

			return extensions;
		}

		private static Task RespondAsync(IOwinContext context, HttpStatusCode statusCode, string message)
		{
			context.Response.StatusCode = (int)statusCode;
			context.Response.ContentType = "text/plain";
			context.Response.Headers[HeaderConstants.TusResumable] = HeaderConstants.TusResumableValue;
			return context.Response.WriteAsync(message);
		}

		private void ValidateConfig()
		{
			if (_config.Store == null)
			{
				throw new TusConfigurationException($"{nameof(_config.Store)} cannot be null.");
			}

			if (string.IsNullOrWhiteSpace(_config.UrlPath))
			{
				throw new TusConfigurationException($"{nameof(_config.UrlPath)} cannot be empty.");
			}
		}
	}
}
