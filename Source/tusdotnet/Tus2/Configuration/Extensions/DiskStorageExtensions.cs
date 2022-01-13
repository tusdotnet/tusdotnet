namespace tusdotnet.Tus2
{
    public static class TusServiceBuilderDiskExtensions
    {
        public static TusServiceBuilder AddDiskStorage(this TusServiceBuilder builder, string diskPath)
        {
            builder.AddStorage(new Tus2DiskStore(new() { DiskPath = diskPath }));
            return builder;
        }

        public static TusServiceBuilder AddDiskStorage(this TusServiceBuilder builder, string name, string diskPath)
        {
            builder.AddStorage(name, new Tus2DiskStore(new() { DiskPath = diskPath }));
            return builder;
        }

        public static TusServiceBuilder AddDiskBasedUploadManager(this TusServiceBuilder builder, string diskPath)
        {
            builder.AddUploadManager(new UploadManagerDiskBased(new() { SharedDiskPath = diskPath }));
            return builder;
        }

        public static TusServiceBuilder AddDiskBasedUploadManager(this TusServiceBuilder builder, string name, string sharedDiskPath)
        {
            builder.AddUploadManager(name, new UploadManagerDiskBased(new() { SharedDiskPath = sharedDiskPath }));
            return builder;
        }
    }
}
