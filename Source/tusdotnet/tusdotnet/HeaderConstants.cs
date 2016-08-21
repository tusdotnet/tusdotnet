namespace tusdotnet
{
	public static class HeaderConstants
	{
		public const string TusResumable = "Tus-Resumable";
		public const string TusResumableValue = "1.0.0";

		public const string TusVersion = "Tus-Version";

		// TODO: Implement, no extensions are currently implemented so this header must be omitted.
		//public const string TusExtension = "Tus-Extension";

		// TODO: Implement at some point, only usable in requests from old browsers.
		//public const string XHttpMethodOveride = "X-HTTP-Method-Override";

		public const string UploadLength = "Upload-Length";
		public const string UploadOffset = "Upload-Offset";

		public const string CacheControl = "Cache-Control";
		public const string NoStore = "no-store";

	}
}
