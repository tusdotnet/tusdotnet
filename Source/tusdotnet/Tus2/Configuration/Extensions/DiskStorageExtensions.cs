namespace tusdotnet.Tus2
{
    public static class TusServiceBuilderDiskExtensions
    {
        public static TusServiceBuilder AddDiskStorage(this TusServiceBuilder builder, string diskPath)
        {
            builder.AddStorage(new Tus2DiskStorage(new() { DiskPath = diskPath }));
            return builder;
        }

        public static TusServiceBuilder AddDiskBasedUploadManager(this TusServiceBuilder builder, string diskPath)
        {
            builder.AddUploadManager(new OngoingUploadManagerDiskBased(new() { SharedDiskPath = diskPath }));
            return builder;
        }
    }
}
