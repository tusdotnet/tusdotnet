namespace tusdotnet.Models
{
    internal class StoreFeatures
    {
        /// <summary>
        /// Supports reading
        /// </summary>
        public bool Readable { get; set; }

        /// <summary>
        /// Supports System.IO.Pipelines
        /// </summary>
        public bool Pipelines { get; set; }
    }
}
