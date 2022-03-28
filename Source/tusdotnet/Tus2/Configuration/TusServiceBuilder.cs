using System;
using System.Collections.Generic;

namespace tusdotnet.Tus2
{
    public class TusServiceBuilder
    {
        public Tus2Storage Storage { get; private set; }

        public ITus2StorageFactory StorageFactory { get; private set; }

        public IOngoingUploadManager OngoingUploadManager { get; private set; }

        public IOngoingUploadManagerFactory OngoingUploadManagerFactory { get; private set; }

        public LinkedList<Type> Handlers { get; private set; }

        public TusServiceBuilder()
        {
            Handlers = new();
        }

        public TusServiceBuilder AddStorage(Tus2Storage storageInstance)
        {
            Storage = storageInstance;
            return this;
        }

        public TusServiceBuilder AddStorageFactory(ITus2StorageFactory factory)
        {
            StorageFactory = factory;
            return this;
        }

        public TusServiceBuilder AddUploadManager(IOngoingUploadManager uploadManager)
        {
            OngoingUploadManager = uploadManager;
            return this;
        }

        public TusServiceBuilder AddUploadManagerFactory(IOngoingUploadManagerFactory factory)
        {
            OngoingUploadManagerFactory = factory;
            return this;
        }

        public TusServiceBuilder AddHandler<T>() where T : TusHandler
        {
            Handlers.AddLast(typeof(T));
            return this;
        }
    }
}
