namespace tusdotnet.Constants
{
	public static class HeaderConstants
	{
		public const string TusResumable = "Tus-Resumable";
		public const string TusResumableValue = "1.0.0";
		public const string TusVersion = "Tus-Version";
		public const string TusExtension = "Tus-Extension";
		public const string TusMaxSize = "Tus-Max-Size";
		public const string TusChecksumAlgorithm = "Tus-Checksum-Algorithm";

		public const string XHttpMethodOveride = "X-HTTP-Method-Override";

		public const string UploadLength = "Upload-Length";

		// TODO: Implement at some point. The disk store does not yet support this.
		//public const string UploadDeferLength = "Upload-Defer-Length";
		public const string UploadOffset = "Upload-Offset";
		public const string UploadMetadata = "Upload-Metadata";
		public const string UploadChecksum = "Upload-Checksum";

		public const string CacheControl = "Cache-Control";
		public const string NoStore = "no-store";
		public const string Location = "location";

	}
}
