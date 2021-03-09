namespace tusdotnet.Stores.BufferHandlers
{
    public struct BufferHandlerCopyResult
    {
        public long BytesWrittenThisRequest { get; set; }

        public bool ClientDisconnectedDuringRead { get; set; }

        public BufferHandlerCopyResult(long bytesWrittenThisRequest, bool clientDisconnectedDuringRead)
        {
            BytesWrittenThisRequest = bytesWrittenThisRequest;
            ClientDisconnectedDuringRead = clientDisconnectedDuringRead;
        }
    }
}