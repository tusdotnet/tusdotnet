#if NET6_0_OR_GREATER

using System.Threading.Tasks;
using tusdotnet.Runners.TusV1Process;

namespace tusdotnet.Runners.Handlers
{
    public abstract class TusV1Handler
    {
        private readonly TusV1ProcessRunner _process;

        // TODO: Probably need to support changing the process after creation to be used by Initialize.
        protected TusV1Handler(TusV1ProcessRunner process)
        {
            _process = process;
        }

        public virtual Task Initialize()
        {
            // TODO: The idea here is to be able to preload data from the store, or use an async method to load the store or similar
            return Task.CompletedTask;
        }

        public virtual Task Finalize()
        {
            // TODO: The idea here is to be able to commit data written to the store or similar
            return Task.CompletedTask;
        }

        public virtual Task<CreateFileResponse> CreateFile(CreateFileRequest request)
        {
            return _process.CreateFile(request);
        }

        public virtual Task<WriteFileResponse> WriteFile(WriteFileRequest request)
        {
            return _process.WriteFile(request);
        }

        public virtual Task<FileInfoResponse> GetFileInfo(FileInfoRequest request)
        {
            return _process.GetFileInfo(request);
        }

        public virtual Task<DeleteFileResponse> DeleteFile(DeleteFileRequest request)
        {
            return _process.DeleteFile(request);
        }
    }
}

#endif