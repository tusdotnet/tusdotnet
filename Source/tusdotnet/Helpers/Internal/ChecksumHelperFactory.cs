using tusdotnet.Adapters;

namespace tusdotnet.Helpers
{
    internal static class ChecksumHelperFactory
    {
        internal static ChecksumHelper Create(ContextAdapter context)
        {
#if trailingheaders
            return new ChecksumHelperWithTrailers(context);
#else
            return new ChecksumHelper(context);
#endif
        }
    }
}
