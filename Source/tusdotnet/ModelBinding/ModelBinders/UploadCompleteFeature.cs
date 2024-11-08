#if NET6_0_OR_GREATER
using tusdotnet.Interfaces;

namespace tusdotnet.ModelBinding.ModelBinders
{
    internal class UploadCompleteFeature
    {
        public UploadCompleteFeature(ITusFile file)
        {
            File = file;
        }

        public ITusFile File { get; }
    }
}
#endif
