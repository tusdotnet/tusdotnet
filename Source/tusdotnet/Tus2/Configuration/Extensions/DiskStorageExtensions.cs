namespace tusdotnet.Tus2
{
    public static class TusServiceBuilderDiskStorageExtensions
    {
        public static TusServiceBuilder AddDiskStorage(this TusServiceBuilder builder, string diskPath)
        {
            builder.AddStorage(new Tus2DiskStore(diskPath));
            builder.AddUploadManager(new UploadManagerDiskBased(diskPath));

            return builder;
        }

        public static TusServiceBuilder AddDiskStorage(this TusServiceBuilder builder, string name, string diskPath)
        {
            builder.AddStorage(name, new Tus2DiskStore(diskPath));
            builder.AddUploadManager(name, new UploadManagerDiskBased(diskPath));

            return builder;
        }
    }
}
