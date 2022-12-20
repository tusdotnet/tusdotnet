#nullable enable
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;

namespace tusdotnet.IntentHandlers
{
    internal class MultiIntentHandler
    {
        private readonly ContextAdapter _context;
        private readonly IntentHandler[] _handlers;

        private readonly ResponseAdapter[] _responseAdapters;

        private int _handlerIndex = -1;

        public IntentHandler? Current { get; private set; }

        public IntentHandler? Previous { get; private set; }

        public MultiIntentHandler(ContextAdapter context, params IntentHandler[] handlers)
        {
            _context = context;
            _handlers = handlers;
            _responseAdapters = new ResponseAdapter[handlers.Length];
        }

        public bool MoveNext()
        {
            Previous = Current;
            _handlerIndex++;

            if (_handlerIndex >= _handlers.Length)
            {
                return false;
            }

            Current = _handlers[_handlerIndex];
            _context.Response = new();
            _responseAdapters[_handlerIndex] = _context.Response;

            ModifyContextForNextIntent(_context, Previous, Current);

            return true;

            static void ModifyContextForNextIntent(ContextAdapter context, IntentHandler? previous, IntentHandler next)
            {
                if (previous is not CreateFileHandler and not ConcatenateFilesHandler)
                    return;

                if (next is not WriteFileHandler)
                    return;

                context.Request.Headers.Remove(HeaderConstants.UploadLength);
                context.Request.Headers[HeaderConstants.UploadOffset] = "0";
            }
        }

        public async Task FinalizeResponse()
        {
            var firstHandler = _handlers[0];
            var firstResponse = _responseAdapters[0];

            IntentHandler? secondHandler = null;
            ResponseAdapter? secondResponse = null;

            if (_handlers.Length > 1)
            {
                secondHandler = _handlers[1];
                secondResponse = _responseAdapters[1];
            }

            _context.Response = await GetFinalResponse(firstHandler, firstResponse, secondHandler, secondResponse);
        }

        private static async Task<ResponseAdapter> GetFinalResponse(IntentHandler firstHandler, ResponseAdapter firstResponse, IntentHandler? secondHandler, ResponseAdapter? secondResponse)
        {
            if (firstHandler is not CreateFileHandler and not ConcatenateFilesHandler)
            {
                return firstResponse;
            }

            if (secondHandler is not WriteFileHandler || secondResponse is null)
            {
                return firstResponse;
            }

            if (firstResponse.Status != System.Net.HttpStatusCode.Created)
            {
                return firstResponse;
            };

            await ModifyFirstResponse(firstResponse, secondHandler, secondResponse);

            return firstResponse;
        }

        private static async Task ModifyFirstResponse(ResponseAdapter firstResponse, IntentHandler secondHandler, ResponseAdapter secondResponse)
        {
            var uploadOffset = await GetUploadOffset(secondResponse, secondHandler.Context);
            firstResponse.SetHeader(HeaderConstants.UploadOffset, uploadOffset.ToString());

            if (secondResponse.Headers.TryGetValue(HeaderConstants.UploadExpires, out var uploadExpires))
            {
                firstResponse.SetHeader(HeaderConstants.UploadExpires, uploadExpires);
            }
        }

        private static async Task<long> GetUploadOffset(ResponseAdapter secondResponse, ContextAdapter context)
        {
            return secondResponse.Headers.TryGetValue(HeaderConstants.UploadOffset, out var uploadOffsetString)
                ? long.Parse(uploadOffsetString)
                : await context.StoreAdapter.GetUploadOffsetAsync(context.FileId, context.CancellationToken);
        }
    }
}
