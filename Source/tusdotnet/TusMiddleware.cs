using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;

namespace tusdotnet
{
	internal static class TusMiddleware
	{
		public static async Task<bool> Invoke(ContextAdapter context)
		{
			ValidateConfig(context.Configuration);

			var request = context.Request;
			var response = context.Response;

			if (!ShouldHandleRequest(context))
			{
				return false;
			}

			var method = request.GetMethod();

			var tusResumable = request.Headers.ContainsKey(HeaderConstants.TusResumable)
				? request.Headers[HeaderConstants.TusResumable].FirstOrDefault()
				: null;

			if (method != "options" && tusResumable != HeaderConstants.TusResumableValue)
			{
				response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
				response.SetHeader(HeaderConstants.TusVersion, HeaderConstants.TusResumableValue);
				await RespondAsync(response,
					HttpStatusCode.PreconditionFailed,
					$"Tus version {tusResumable} is not supported. Supported versions: {HeaderConstants.TusResumableValue}");
				return true;
			}

			switch (method)
			{
				case "post":
					return await HandlePostRequest(context);
				case "head":
					return await HandleHeadRequest(context);
				case "patch":
					return await HandlePatchRequest(context);
				case "options":
					return await HandleOptionsRequest(context);
				case "delete":
					return await HandleDeleteRequest(context);
				default:
					throw new NotImplementedException($"HTTP method {method} is not implemented.");
			}
		}

		private static async Task<bool> HandleDeleteRequest(ContextAdapter context)
		{
			/* When receiving a DELETE request for an existing upload the Server SHOULD free associated resources and MUST 
			 * respond with the 204 No Content status confirming that the upload was terminated. 
			 * For all future requests to this URL the Server SHOULD respond with the 404 Not Found or 410 Gone status.
			 * */

			var store = context.Configuration.Store as ITusTerminationStore;
			if (store == null)
			{
				return false;
			}

			var response = context.Response;
			var cancellationToken = context.CancellationToken;

			var fileId = GetFileName(context);
			var fileLock = new FileLock(fileId);

			var hasLock = fileLock.Lock(cancellationToken);
			if (!hasLock)
			{
				await
					RespondAsync(response, HttpStatusCode.Conflict,
						$"File {fileId} is currently being updated. Please try again later");
				return true;
			}

			try
			{
				var exists = await context.Configuration.Store.FileExistAsync(fileId, cancellationToken);
				if (!exists)
				{
					response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
					response.SetHeader(HeaderConstants.CacheControl, HeaderConstants.NoStore);
					response.SetStatus((int)HttpStatusCode.NotFound);
					return true;
				}

				await store.DeleteFileAsync(fileId, cancellationToken);
				response.SetStatus((int)HttpStatusCode.NoContent);
				response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
			}
			finally
			{
				fileLock.ReleaseIfHeld();
			}

			return true;
		}

		private static async Task<bool> HandlePostRequest(ContextAdapter context)
		{
			/*
			 * Creation:
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
			 * 
			 * Concatenation:
			 * This extension can be used to concatenate multiple uploads into a single one enabling Clients to perform parallel uploads and 
			 * to upload non-contiguous chunks. If the Server supports this extension, it MUST add concatenation to the Tus-Extension header.
			 * A partial upload represents a chunk of a file. It is constructed by including the Upload-Concat: partial header 
			 * while creating a new upload using the Creation extension. Multiple partial uploads are concatenated into a 
			 * final upload in the specified order. The Server SHOULD NOT process these partial uploads until they are 
			 * concatenated to form a final upload. The length of the final upload MUST be the sum of the length of all partial uploads.
			 * In order to create a new final upload the Client MUST add the Upload-Concat header to the upload creation request. 
			 * The value MUST be final followed by a semicolon and a space-separated list of the partial upload URLs that need to be concatenated. 
			 * The partial uploads MUST be concatenated as per the order specified in the list. 
			 * This concatenation request SHOULD happen after all of the corresponding partial uploads are completed.
			 * The Client MUST NOT include the Upload-Length header in the final upload creation.
			 * The Client MAY send the concatenation request while the partial uploads are still in progress.
			 * This feature MUST be explicitly announced by the Server by adding concatenation-unfinished to the Tus-Extension header.
			 * When creating a new final upload the partial uploads’ metadata SHALL NOT be transferred to the new final upload.
			 * All metadata SHOULD be included in the concatenation request using the Upload-Metadata header.
			 * The Server MAY delete partial uploads after concatenation. They MAY however be used multiple times to form a final resource.
			 */

			var tusCreationStore = context.Configuration.Store as ITusCreationStore;
			if (tusCreationStore == null)
			{
				return false;
			}

			var request = context.Request;
			var response = context.Response;
			var cancellationToken = context.CancellationToken;

			var tusConcatenationStore = context.Configuration.Store as ITusConcatenationStore;
			UploadConcat uploadConcat = null;
			if (tusConcatenationStore != null && request.Headers.ContainsKey(HeaderConstants.UploadConcat))
			{
				uploadConcat = new UploadConcat(request.Headers[HeaderConstants.UploadConcat].First(), context.Configuration.UrlPath);
				if (!uploadConcat.IsValid)
				{
					await RespondAsync(response, HttpStatusCode.BadRequest, uploadConcat.ErrorMessage);
					return true;
				}
			}

			var uploadLength = -1L;
			if (!(uploadConcat?.Type is FileConcatFinal))
			{
				if (!request.Headers.ContainsKey(HeaderConstants.UploadLength))
				{
					await RespondAsync(response, HttpStatusCode.BadRequest, $"Missing {HeaderConstants.UploadLength} header");
					return true;
				}

				if (!long.TryParse(request.Headers[HeaderConstants.UploadLength].First(), out uploadLength))
				{
					await RespondAsync(response, HttpStatusCode.BadRequest, $"Could not parse {HeaderConstants.UploadLength}");
					return true;
				}

				if (uploadLength < 0)
				{
					await RespondAsync(response,
						HttpStatusCode.BadRequest,
						$"Header {HeaderConstants.UploadLength} must be a positive number");
					return true;
				}

				if (context.Configuration.MaxAllowedUploadSizeInBytes.HasValue && uploadLength > context.Configuration.MaxAllowedUploadSizeInBytes.Value)
				{
					await RespondAsync(response,
						HttpStatusCode.RequestEntityTooLarge,
						$"Header {HeaderConstants.UploadLength} exceeds the server's max file size.");
					return true;
				}
			}

			string metadata = null;
			if (request.Headers.ContainsKey(HeaderConstants.UploadMetadata))
			{
				var validateMetadataResult =
					Metadata.ValidateMetadataHeader(request.Headers[HeaderConstants.UploadMetadata].First());
				if (!string.IsNullOrEmpty(validateMetadataResult))
				{
					await RespondAsync(response, HttpStatusCode.BadRequest, validateMetadataResult);
					return true;
				}

				metadata = request.Headers[HeaderConstants.UploadMetadata].FirstOrDefault();
			}

			string fileId = null;
			try
			{
				if (tusConcatenationStore != null && uploadConcat != null)
				{
					if (uploadConcat.Type is FileConcatPartial)
					{
						fileId = await tusConcatenationStore
								.CreatePartialFileAsync(uploadLength, metadata, cancellationToken);

					}
					else if (uploadConcat.Type is FileConcatFinal)
					{
						var finalConcat = (FileConcatFinal)uploadConcat.Type;
						var filesExist =
								await Task.WhenAll(finalConcat.Files.Select(f => context.Configuration.Store.FileExistAsync(f, cancellationToken)));

						if (filesExist.Any(f => !f))
						{
							await RespondAsync(response,
								HttpStatusCode.BadRequest,
								$"Could not find some of the files supplied for concatenation: {string.Join(", ", filesExist.Zip(finalConcat.Files, (b, s) => new { exist = b, name = s }).Where(f => !f.exist).Select(f => f.name))}");
							return true;
						}

						var filesArePartial = await Task.WhenAll(finalConcat.Files.Select(f =>
							tusConcatenationStore.GetUploadConcatAsync(f, cancellationToken)));

						if (filesArePartial.Any(f => !(f is FileConcatPartial)))
						{
							await RespondAsync(response,
								HttpStatusCode.BadRequest,
								$"Some of the files supplied for concatenation are not marked as partial and can not be concatenated: {string.Join(", ", filesArePartial.Zip(finalConcat.Files, (s, s1) => new { partial = s is FileConcatPartial, name = s1 }).Where(f => !f.partial).Select(f => f.name))}"
							);
							return true;
						}

						var incompleteFiles = new List<string>();
						var totalSize = 0L;
						foreach (var file in finalConcat.Files)
						{
							var length = context.Configuration.Store.GetUploadLengthAsync(file, cancellationToken);
							var offset = context.Configuration.Store.GetUploadOffsetAsync(file, cancellationToken);
							await Task.WhenAll(length, offset);

							if (length.Result != null)
							{
								totalSize += length.Result.Value;
							}

							if (length.Result != offset.Result)
							{
								incompleteFiles.Add(file);
							}
						}

						if (incompleteFiles.Any())
						{
							await RespondAsync(response,
								HttpStatusCode.BadRequest,
								$"Some of the files supplied for concatenation are not finished and can not be concatenated: {string.Join(", ", incompleteFiles)}");
							return true;
						}

						if (context.Configuration.MaxAllowedUploadSizeInBytes.HasValue && totalSize > context.Configuration.MaxAllowedUploadSizeInBytes.Value)
						{
							await RespondAsync(response,
								HttpStatusCode.RequestEntityTooLarge,
								"The concatenated file exceeds the server's max file size.");
							return true;
						}

						fileId = await tusConcatenationStore.CreateFinalFileAsync(finalConcat.Files, metadata,
							cancellationToken);

						// Run callback that the final file is completed.
						if (context.Configuration.OnUploadCompleteAsync != null)
						{
							await context.Configuration.OnUploadCompleteAsync(fileId, context.Configuration.Store, cancellationToken);
						}
					}
				}
				else
				{
					fileId = await tusCreationStore.CreateFileAsync(uploadLength, metadata, cancellationToken);
				}
			}
			catch (TusStoreException storeException)
			{
				await RespondAsync(response, HttpStatusCode.BadRequest, storeException.Message);
				return true;
			}

			response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
			response.SetHeader(HeaderConstants.Location, $"{context.Configuration.UrlPath.TrimEnd('/')}/{fileId}");
			response.SetStatus((int)HttpStatusCode.Created);
			return true;
		}

		private static async Task<bool> HandleOptionsRequest(ContextAdapter context)
		{
			/*
			 * An OPTIONS request MAY be used to gather information about the Server’s current configuration. 
			 * A successful response indicated by the 204 No Content status MUST contain the Tus-Version header. 
			 * It MAY include the Tus-Extension and Tus-Max-Size headers.
			 * The Client SHOULD NOT include the Tus-Resumable header in the request and the Server MUST discard it.
			 * */

			var response = context.Response;
			var cancellationToken = context.CancellationToken;

			response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
			response.SetHeader(HeaderConstants.TusVersion, HeaderConstants.TusResumableValue);

			if (context.Configuration.MaxAllowedUploadSizeInBytes.HasValue)
			{
				response.SetHeader(HeaderConstants.TusMaxSize, context.Configuration.MaxAllowedUploadSizeInBytes.Value.ToString());
			}

			var extensions = DetectExtensions(context);
			if (extensions.Any())
			{
				response.SetHeader(HeaderConstants.TusExtension, string.Join(",", extensions));
			}

			var checksumStore = context.Configuration.Store as ITusChecksumStore;
			if (checksumStore != null)
			{
				var checksumAlgorithms = await checksumStore.GetSupportedAlgorithmsAsync(cancellationToken);
				response.SetHeader(HeaderConstants.TusChecksumAlgorithm, string.Join(",", checksumAlgorithms));
			}

			response.SetStatus((int)HttpStatusCode.NoContent);
			return true;
		}

		private static async Task<bool> HandlePatchRequest(ContextAdapter context)
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
			 * 
			 * Concatenation:
			 * The Server MUST respond with the 403 Forbidden status to PATCH requests against a final upload URL and 
			 * MUST NOT modify the final or its partial uploads.
			 * */

			var request = context.Request;
			var response = context.Response;
			var cancellationToken = context.CancellationToken;

			var fileName = GetFileName(context);
			var fileLock = new FileLock(fileName);
			var checksumStore = context.Configuration.Store as ITusChecksumStore;
			var providedChecksum = request.Headers.ContainsKey(HeaderConstants.UploadChecksum)
				? new Checksum(request.Headers[HeaderConstants.UploadChecksum].First())
				: null;

			var hasLock = fileLock.Lock(cancellationToken);

			if (!hasLock)
			{
				await
					RespondAsync(response, HttpStatusCode.Conflict,
						$"File {fileName} is currently being updated. Please try again later");
				return true;
			}

			try
			{
				var concatStore = context.Configuration.Store as ITusConcatenationStore;
				FileConcat uploadConcat = null;
				if (concatStore != null)
				{
					uploadConcat = await concatStore.GetUploadConcatAsync(fileName, cancellationToken);

					if (uploadConcat is FileConcatFinal)
					{
						await RespondAsync(response, HttpStatusCode.Forbidden, "File with \"Upload-Concat: final\" cannot be patched");
						return true;
					}
				}

				if (request.ContentType == null ||
					!request.ContentType.Equals("application/offset+octet-stream",
						StringComparison.OrdinalIgnoreCase))
				{
					await RespondAsync(response,
						HttpStatusCode.BadRequest,
						$"Content-Type {request.ContentType} is invalid. Must be application/offset+octet-stream");
					return true;
				}

				if (!request.Headers.ContainsKey(HeaderConstants.UploadOffset))
				{
					await RespondAsync(response, HttpStatusCode.BadRequest, $"Missing {HeaderConstants.UploadOffset} header");
					return true;
				}

				long requestOffset;

				if (!long.TryParse(request.Headers[HeaderConstants.UploadOffset].FirstOrDefault(), out requestOffset))
				{
					await RespondAsync(response, HttpStatusCode.BadRequest, $"Could not parse {HeaderConstants.UploadOffset} header");
					return true;
				}

				if (requestOffset < 0)
				{
					await RespondAsync(response, HttpStatusCode.BadRequest,
						$"Header {HeaderConstants.UploadOffset} must be a positive number");
					return true;
				}

				if (checksumStore != null && providedChecksum != null)
				{
					if (!providedChecksum.IsValid)
					{
						await RespondAsync(response, HttpStatusCode.BadRequest, $"Could not parse {HeaderConstants.UploadChecksum} header");
						return true;
					}

					var checksumAlgorithms = (await checksumStore.GetSupportedAlgorithmsAsync(cancellationToken)).ToList();
					if (!checksumAlgorithms.Contains(providedChecksum.Algorithm))
					{
						await RespondAsync(response, HttpStatusCode.BadRequest,
							$"Unsupported checksum algorithm. Supported algorithms are: {string.Join(",", checksumAlgorithms)}");
						return true;
					}
				}

				var exists = await context.Configuration.Store.FileExistAsync(fileName, cancellationToken);
				if (!exists)
				{
					response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
					response.SetHeader(HeaderConstants.CacheControl, HeaderConstants.NoStore);
					response.SetStatus((int)HttpStatusCode.NotFound);
					return true;
				}

				var fileOffset = await context.Configuration.Store.GetUploadOffsetAsync(fileName, cancellationToken);

				if (requestOffset != fileOffset)
				{
					await RespondAsync(response,
						HttpStatusCode.Conflict,
						$"Offset does not match file. File offset: {fileOffset}. Request offset: {requestOffset}");
					return true;
				}

				var fileUploadLength = await context.Configuration.Store.GetUploadLengthAsync(fileName, cancellationToken);

				if (fileUploadLength != null && fileOffset == fileUploadLength.Value)
				{
					await RespondAsync(response, HttpStatusCode.BadRequest, "Upload is already complete.");
					return true;
				}

				long bytesWritten;
				try
				{
					bytesWritten = await context.Configuration.Store.AppendDataAsync(fileName, request.Body, cancellationToken);
				}
				catch (IOException ioException)
				{
					// Indicates that the client disconnected.
					if (!ioException.ClientDisconnected())
					{
						throw;
					}

					// Client disconnected so no need to return a response.
					return true;
				}
				catch (TusStoreException storeException)
				{
					await RespondAsync(response, HttpStatusCode.BadRequest, storeException.Message);
					throw;
				}

				if (checksumStore != null && providedChecksum != null)
				{
					var validChecksum = await checksumStore
						.VerifyChecksumAsync(fileName, providedChecksum.Algorithm, providedChecksum.Hash, cancellationToken);

					if (!validChecksum)
					{
						await RespondAsync(response, (HttpStatusCode)460, "Header Upload-Checksum does not match the checksum of the file");
						return true;
					}
				}

				response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
				response.SetHeader(HeaderConstants.UploadOffset, (fileOffset + bytesWritten).ToString());
				response.SetStatus((int)HttpStatusCode.NoContent);

				// Run OnUploadComplete if it has been provided.
				var fileIsComplete = fileUploadLength != null && (fileOffset + bytesWritten) == fileUploadLength.Value;
				if (fileIsComplete && !(uploadConcat is FileConcatPartial) && context.Configuration.OnUploadCompleteAsync != null)
				{
					await context.Configuration.OnUploadCompleteAsync(fileName, context.Configuration.Store, cancellationToken);
				}
			}
			finally
			{
				fileLock.ReleaseIfHeld();
			}

			return true;
		}

		private static async Task<bool> HandleHeadRequest(ContextAdapter context)
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
			 * 
			 * Concatenation:
			 * The response to a HEAD request for a final upload SHOULD NOT contain the Upload-Offset header unless the 
			 * concatenation has been successfully finished. After successful concatenation, the Upload-Offset and Upload-Length 
			 * MUST be set and their values MUST be equal. The value of the Upload-Offset header before concatenation is not 
			 * defined for a final upload. The response to a HEAD request for a partial upload MUST contain the Upload-Offset header.
			 * The Upload-Length header MUST be included if the length of the final resource can be calculated at the time of the request. 
			 * Response to HEAD request against partial or final upload MUST include the Upload-Concat header and its value as received 
			 * in the upload creation request.
			 */

			var response = context.Response;
			var cancellationToken = context.CancellationToken;

			var fileName = GetFileName(context);

			var exists = await context.Configuration.Store.FileExistAsync(fileName, cancellationToken);
			if (!exists)
			{
				response.SetHeader(HeaderConstants.CacheControl, HeaderConstants.NoStore);
				response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
				response.SetStatus((int)HttpStatusCode.NotFound);
				return true;
			}

			var uploadLength = await context.Configuration.Store.GetUploadLengthAsync(fileName, cancellationToken);
			if (uploadLength != null)
			{
				response.SetHeader(HeaderConstants.UploadLength, uploadLength.Value.ToString());
			}

			var tusCreationStore = context.Configuration.Store as ITusCreationStore;
			if (tusCreationStore != null)
			{
				var uploadMetadata = await tusCreationStore.GetUploadMetadataAsync(fileName, cancellationToken);
				if (!string.IsNullOrEmpty(uploadMetadata))
				{
					response.SetHeader(HeaderConstants.UploadMetadata, uploadMetadata);
				}
			}

			var uploadOffset = await context.Configuration.Store.GetUploadOffsetAsync(fileName, cancellationToken);

			var tusConcatStore = context.Configuration.Store as ITusConcatenationStore;
			FileConcat uploadConcat = null;
			var addUploadOffset = true;
			if (tusConcatStore != null)
			{
				uploadConcat = await tusConcatStore.GetUploadConcatAsync(fileName, cancellationToken);

				// Only add Upload-Offset to final files if they are complete.
				if (uploadConcat is FileConcatFinal && uploadLength != uploadOffset)
				{
					addUploadOffset = false;
				}
			}

			response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
			if (addUploadOffset)
			{
				response.SetHeader(HeaderConstants.UploadOffset, uploadOffset.ToString());
			}
			response.SetHeader(HeaderConstants.CacheControl, HeaderConstants.NoStore);
			if (uploadConcat != null)
			{
				(uploadConcat as FileConcatFinal)?.AddUrlPathToFiles(context.Configuration.UrlPath);
				response.SetHeader(HeaderConstants.UploadConcat, uploadConcat.GetHeader());
			}

			return true;
		}

		private static bool ShouldHandleRequest(ContextAdapter context)
		{
			var request = context.Request;
			var method = request.GetMethod();

			if (!request.Headers.ContainsKey(HeaderConstants.TusResumable) && method != "options")
			{
				return false;
			}

			switch (method)
			{
				case "post":
				case "options":
					return IsExactUrlMatch(context);
				case "head":
				case "patch":
				case "delete":
					return !IsExactUrlMatch(context) &&
						   request.RequestUri.LocalPath.StartsWith(context.Configuration.UrlPath, StringComparison.OrdinalIgnoreCase);
				default:
					return false;
			}
		}

		private static string GetFileName(ContextAdapter context)
		{
			var request = context.Request;
			var startIndex = request
								 .RequestUri
								 .LocalPath
								 .IndexOf(context.Configuration.UrlPath, StringComparison.OrdinalIgnoreCase) + context.Configuration.UrlPath.Length;
			return request
				.RequestUri
				.LocalPath
				.Substring(startIndex)
				.Trim('/');
		}

		private static bool IsExactUrlMatch(ContextAdapter context)
		{
			return context.Request.RequestUri.LocalPath.TrimEnd('/') == context.Configuration.UrlPath.TrimEnd('/');
		}

		private static List<string> DetectExtensions(ContextAdapter context)
		{
			var extensions = new List<string>();
			if (context.Configuration.Store is ITusCreationStore)
			{
				extensions.Add(ExtensionConstants.Creation);
			}

			if (context.Configuration.Store is ITusTerminationStore)
			{
				extensions.Add(ExtensionConstants.Termination);
			}

			if (context.Configuration.Store is ITusChecksumStore)
			{
				extensions.Add(ExtensionConstants.Checksum);
			}

			if (context.Configuration.Store is ITusConcatenationStore)
			{
				extensions.Add(ExtensionConstants.Concatenation);
			}

			return extensions;
		}

		private static Task RespondAsync(ResponseAdapter response, HttpStatusCode statusCode, string message)
		{
			response.SetHeader(HeaderConstants.ContentType, "text/plain");
			response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
			response.SetStatus((int)statusCode);
			var buffer = new UTF8Encoding().GetBytes(message);
			return response.Body.WriteAsync(buffer, 0, buffer.Length);
		}

		private static void ValidateConfig(ITusConfiguration config)
		{
			if (config.Store == null)
			{
				throw new TusConfigurationException($"{nameof(config.Store)} cannot be null.");
			}

			if (string.IsNullOrWhiteSpace(config.UrlPath))
			{
				throw new TusConfigurationException($"{nameof(config.UrlPath)} cannot be empty.");
			}
		}
	}
}
