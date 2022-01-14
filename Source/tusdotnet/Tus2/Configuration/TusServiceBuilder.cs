using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public class TusServiceBuilder
    {
        public Dictionary<string, Func<HttpContext, Task<ITus2Storage>>> NamedStorage { get; }

        public Dictionary<string, Func<HttpContext, Task<IOngoingUploadManager>>> NamedUploadManager { get; }

        public ITus2Storage Storage { get; private set; }

        public Func<HttpContext, Task<ITus2Storage>> StorageFactory { get; private set; }

        public IOngoingUploadManager UploadManager { get; private set; }

        public Func<HttpContext, Task<IOngoingUploadManager>> UploadManagerFactory { get; private set; }

        public LinkedList<Type> Handlers { get; private set; }

        public TusServiceBuilder()
        {
            NamedStorage = new();
            NamedUploadManager = new();
            Handlers = new();
        }

        public TusServiceBuilder AddStorage(ITus2Storage storageInstance)
        {
            Storage = storageInstance;
            return this;
        }

        public TusServiceBuilder AddStorage(Func<HttpContext, Task<ITus2Storage>> storageFactory)
        {
            StorageFactory = storageFactory;
            return this;
        }

        public TusServiceBuilder AddStorage(string name, ITus2Storage storageInstance)
        {
            NamedStorage.Add(name, _ => Task.FromResult(storageInstance));
            return this;
        }

        public TusServiceBuilder AddStorage(string name, Func<HttpContext, Task<ITus2Storage>> storageFactory)
        {
            NamedStorage.Add(name, storageFactory);
            return this;
        }

        public TusServiceBuilder AddUploadManager(IOngoingUploadManager uploadManager)
        {
            UploadManager = uploadManager;
            return this;
        }

        public TusServiceBuilder AddUploadManager(Func<HttpContext, Task<IOngoingUploadManager>> uploadManagerFactory)
        {
            UploadManagerFactory = uploadManagerFactory;
            return this;
        }

        public TusServiceBuilder AddUploadManager(string name, IOngoingUploadManager uploadManager)
        {
            NamedUploadManager.Add(name, _ => Task.FromResult(uploadManager));
            return this;
        }

        public TusServiceBuilder AddUploadManager(string name, Func<HttpContext, Task<IOngoingUploadManager>> uploadManagerFactory)
        {
            NamedUploadManager.Add(name, uploadManagerFactory);
            return this;
        }

        public TusServiceBuilder AddHandler<T>() where T : TusHandler
        {
            Handlers.AddLast(typeof(T));
            return this;
        }
    }
}
