#if NET6_0_OR_GREATER
using tusdotnet.Interfaces;

namespace tusdotnet.ModelBinders
{
    internal class ResumableUploadCompleteFeature
    {
        public ResumableUploadCompleteFeature(ITusFile file)
        {
            File = file;
        }

        public ITusFile File { get; }
    }
}
#endif