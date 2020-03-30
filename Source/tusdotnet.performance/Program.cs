using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.performance
{
    public static class Program
    {
        private const int NUMBER_OF_CLIENTS = 10;
        private const int NUMBER_OF_FILES_TO_UPLOAD = 100;
        private const string SERVER_URL = "https://localhost:5007";
        private static readonly Random _random = new Random();

        public static async Task Main()
        {
            Console.WriteLine($"Starting performance test using {NUMBER_OF_CLIENTS} clients and {NUMBER_OF_FILES_TO_UPLOAD} files each.");
            Console.WriteLine("Server base url is " + SERVER_URL);
            Console.WriteLine();
            Console.WriteLine("NOTE: This app will not output anything until completed to not impact performance. See log from server for trace.");
            Console.WriteLine();

            var clients = new List<Task>(NUMBER_OF_CLIENTS);
            for (int i = 0; i < NUMBER_OF_CLIENTS; i++)
            {
                clients.Add(Task.Run(RunPerfTest));
            }

            await Task.WhenAll(clients).ConfigureAwait(false);

            Console.WriteLine("Test completed. Press any key to exit.");
            Console.ReadKey(true);
        }

        private static async Task RunPerfTest()
        {
            var httpClient = new HttpClient()
            {
                BaseAddress = new Uri(SERVER_URL)
            };

            var file = new byte[_random.Next(5000, 50000)];
            _random.NextBytes(file);

            var halfFileSize = (int)Math.Floor((decimal)file.Length / 2);

            for (int i = 0; i < NUMBER_OF_FILES_TO_UPLOAD; i++)
            {
                // Create file
                var response = await httpClient.SendAsync(GetCreateFileRequest(file)).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var fileLocation = response.Headers.Location;

                // Send first half of file to emulate a disconnect
                response = await httpClient.SendAsync(GetWriteFileRequest(file, 0, halfFileSize, fileLocation)).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                // Get the file info to continue the upload
                response = await httpClient.SendAsync(CreateTusResumableRequest(HttpMethod.Head, fileLocation)).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                // Upload second half of file
                var uploadOffset = int.Parse(response.Headers.GetValues("Upload-Offset").First());
                response = await httpClient.SendAsync(GetWriteFileRequest(file, uploadOffset, file.Length - uploadOffset, fileLocation)).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
        }

        private static HttpRequestMessage GetWriteFileRequest(byte[] file, int offset, int count, Uri fileLocation)
        {
            var writeFileRequest = CreateTusResumableRequest(HttpMethod.Patch, fileLocation);
            writeFileRequest.Headers.Add("Upload-Offset", offset.ToString());
            writeFileRequest.Content = new ByteArrayContent(file, offset, count);
            writeFileRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/offset+octet-stream");
            return writeFileRequest;
        }

        private static HttpRequestMessage GetCreateFileRequest(byte[] file)
        {
            var createFileRequest = CreateTusResumableRequest(HttpMethod.Post, new Uri("/files", UriKind.Relative));
            createFileRequest.Headers.Add("Upload-Length", file.Length.ToString());
            createFileRequest.Headers.Add("Upload-Metadata", CreateRandomMetadata());
            return createFileRequest;
        }

        private static HttpRequestMessage CreateTusResumableRequest(HttpMethod method, Uri relativeUri)
        {
            var request = new HttpRequestMessage(method, relativeUri);
            request.Headers.Add("Tus-Resumable", "1.0.0");

            return request;
        }

        private static string CreateRandomMetadata()
        {
            var filename = Convert.ToBase64String(Encoding.UTF8.GetBytes("filename" + Guid.NewGuid().ToString()));
            var contentType = Convert.ToBase64String(Encoding.UTF8.GetBytes("application/octet-stream"));

            return $"filename {filename},contentType {contentType}";
        }
    }
}
