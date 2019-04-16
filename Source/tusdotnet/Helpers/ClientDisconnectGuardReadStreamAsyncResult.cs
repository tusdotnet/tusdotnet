namespace tusdotnet.Helpers
{
    internal readonly struct ClientDisconnectGuardReadStreamAsyncResult
    {
        internal int BytesRead { get; }

        internal bool ClientDisconnected { get; }

        internal ClientDisconnectGuardReadStreamAsyncResult(bool clientDisconnected, int bytesRead)
        {
            ClientDisconnected = clientDisconnected;
            BytesRead = bytesRead;
        }
    }
}
