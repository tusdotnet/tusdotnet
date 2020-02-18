namespace tusdotnet.Models
{
    /// <summary>
    /// The different strategies to use when parsing metadata.
    /// </summary>
    public enum MetadataParsingStrategy
    {
        /// <summary>
        /// Both key/value pairs and keys without values are allowed.
        /// <para>
        /// Each metadata item can consist of a key and a base64 encoded value or only a key. An empty <c>Upload-Metadata</c> will be considered the same as not providing the header at all.
        /// </para>
        /// <para>
        /// Example: <c>name dGVzdC50eHQ=,myEmptyMeta,contentType dGV4dC9wbGFpbg==</c>
        /// </para>
        /// </summary>
        AllowEmptyValues,

        /// <summary>
        /// Key/value pair based metadata.
        /// <para>
        /// Each metadata item must consist of a key and a base64 encoded value. An empty <c>Upload-Metadata</c> header is not allowed and will result in an error to the client.
        /// </para>
        /// <para>
        /// Example: <c>name dGVzdC50eHQ=,contentType dGV4dC9wbGFpbg==</c>
        /// </para>
        /// </summary>
        Original
    }
}
