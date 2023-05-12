#if NET6_0_OR_GREATER
using System;

namespace tusdotnet.ModelBinders
{
    internal class ResumableUploadParameterInfo
    {
        public Type TypeOfResumableUploadParam { get; set; }
    }
}
#endif