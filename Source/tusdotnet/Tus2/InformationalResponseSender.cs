﻿#nullable disable
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace tusdotnet.Tus2
{
    internal static class InformationalResponseSender
    {
        public static async Task Send104UploadResumptionSupported(
            this HttpContext httpContext,
            string location,
            TusHandlerLimits? limits
        )
        {
            var headers = new Dictionary<string, string>
            {
                { "Location", location },
                { "upload-draft-interop-version", DraftInteropVersion.Version }
            };

            if (limits is not null)
                headers.Add("Upload-Limit", limits.ToSfDictionary());

            switch (httpContext.Request.Protocol)
            {
                case "HTTP/1.0"
                or "HTTP/1.1":
                    await Http1xWriter.Send104UploadResumptionSupported(httpContext, headers);
                    break;
                case "HTTP/2": // or "HTTP/3":
                    Http2And3Writer.Send104UploadResumptionSupported(httpContext, headers);
                    break;
            }
        }

        public static async Task Send104UploadResumptionSupported(
            this HttpContext httpContext,
            long uploadOffset
        )
        {
            var headers = new Dictionary<string, string>
            {
                { "upload-offset", uploadOffset.ToString() },
                { "upload-draft-interop-version", DraftInteropVersion.Version }
            };

            switch (httpContext.Request.Protocol)
            {
                case "HTTP/1.0"
                or "HTTP/1.1":
                    await Http1xWriter.Send104UploadResumptionSupported(httpContext, headers);
                    break;
                case "HTTP/2": // or "HTTP/3":
                    Http2And3Writer.Send104UploadResumptionSupported(httpContext, headers);
                    break;
            }
        }

        private static class Http1xWriter
        {
            private const string _http10FormatLocation =
                "HTTP/1.0 104 Upload Resumption Supported\r\n{0}\r\n";
            private const string _http11FormatLocation =
                "HTTP/1.1 104 Upload Resumption Supported\r\n{0}\r\n";

            private delegate ValueTask<FlushResult> WriteDataToPipeAsync(
                ReadOnlySpan<byte> buffer,
                CancellationToken cancellationToken = default
            );

            public static async Task Send104UploadResumptionSupported(
                HttpContext httpContext,
                Dictionary<string, string> headers
            )
            {
                const string outputProducerName = "Output";
                const string writeDataMethodName = "WriteDataToPipeAsync";

                var httpConnection = httpContext.Features.Get<IHttpConnectionFeature>();
                var output = httpConnection
                    .GetType()
                    .GetProperty(outputProducerName)
                    .GetValue(httpConnection);
                var method = output.GetType().GetMethod(writeDataMethodName);

                var writeToPipe = method.CreateDelegate<WriteDataToPipeAsync>(output);

                string message = GetMessageToSend(httpContext.Request.Protocol, headers);
                var bytes = Encoding.UTF8.GetBytes(message);

                await writeToPipe(bytes.AsSpan());
            }

            private static string GetMessageToSend(
                string protocol,
                Dictionary<string, string> headers
            )
            {
                string format;
                if (protocol == "HTTP/1.0")
                {
                    format = _http10FormatLocation;
                }
                else
                {
                    format = _http11FormatLocation;
                }

                var sb = new StringBuilder();
                foreach (var item in headers)
                {
                    sb.Append(item.Key);
                    sb.Append(':');
                    sb.Append(item.Value);
                    sb.Append("\r\n");
                }

                return string.Format(format, sb.ToString());
            }
        }

        private static class Http2And3Writer
        {
            private const BindingFlags BindingFlagsPrivate =
                BindingFlags.Instance | BindingFlags.NonPublic;

            private const int UploadResumptionSupportedHttpStatusCode = 104;

            private static readonly object _endHeadersFlag = LoadEndHeadersFlag();

            public static void Send104UploadResumptionSupported(
                HttpContext httpContext,
                Dictionary<string, string> headers
            )
            {
                var output = GetOutputProducer(httpContext);

                var internalHeaders = GetHeaderDictionary();
                foreach (var item in headers)
                {
                    internalHeaders.Add(item.Key, new(item.Value));
                }

                Write(
                    output,
                    UploadResumptionSupportedHttpStatusCode,
                    httpContext.Request.Protocol,
                    internalHeaders
                );
            }

            private static void Write(
                object outputProducer,
                int statusCode,
                string httpProtocol,
                IHeaderDictionary headers
            )
            {
                const string FrameWriterName = "_frameWriter";
                const string WriteResponseHeadersName = "WriteResponseHeaders";
                const string StreamIdName = "StreamId";

                var outputProducerType = outputProducer.GetType();

                var writer = outputProducerType
                    .GetField(FrameWriterName, BindingFlagsPrivate)
                    .GetValue(outputProducer);

                // Methods are different between HTTP/2 and HTTP/3.
                if (httpProtocol == "HTTP/2")
                {
                    // We need the stream id here while HTTP/3 always writes to the current stream.
                    var streamId = (int)
                        outputProducerType
                            .GetProperty(StreamIdName, BindingFlagsPrivate)
                            .GetValue(outputProducer);
                    var writeResponseHeaders = writer.GetType().GetMethod(WriteResponseHeadersName);
                    writeResponseHeaders.Invoke(
                        writer,
                        new object[] { streamId, statusCode, _endHeadersFlag, headers }
                    );
                }

                // TODO: Make it work over HTTP/3...
                // The code below does not throw but causes "Failure when receiving data from the peer" in cURL
                // Since H3 is experimental in dotnet there's no need to spend more time on it now.
                //else if (httpProtocol == "HTTP/3")
                //{
                //    var writeResponseHeaders = writer.GetType().GetMethod(WriteResponseHeadersName, BindingFlagsPrivate);
                //    writeResponseHeaders.Invoke(writer, new object[] { statusCode, headers });
                //}
                //else
                //{
                //    throw new NotImplementedException();
                //}
            }

            private static IHeaderDictionary GetHeaderDictionary()
            {
                const string HttpResponseHeadersName =
                    "Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpResponseHeaders";

                var headersType = GetKestrelCoreAssembly().GetType(HttpResponseHeadersName);
                var ctor = headersType.GetConstructor(new[] { typeof(Func<string, Encoding?>) });
                var headers = ctor.Invoke(new object[] { null });

                return (IHeaderDictionary)headers;
            }

            private static object GetOutputProducer(HttpContext httpContext)
            {
                const string OutputProducerName = "Output";

                var httpConnection = httpContext.Features.Get<IHttpConnectionFeature>();
                var outputProducer = httpConnection
                    .GetType()
                    .GetProperty(OutputProducerName)
                    ?.GetValue(httpConnection);

                return outputProducer;
            }

            private static object LoadEndHeadersFlag()
            {
                const string Http2HeadersFrameFlagsName =
                    "Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http2.Http2HeadersFrameFlags";
                const string EndHeadersName = "END_HEADERS";

                var type = GetKestrelCoreAssembly().GetType(Http2HeadersFrameFlagsName);

                return Enum.Parse(type, EndHeadersName);
            }

            private static Assembly GetKestrelCoreAssembly()
            {
                // Http2Limits is in the same assembly as internal core classes of Kestrel that we need.
                return typeof(Http2Limits).Assembly;
            }
        }
    }
}
