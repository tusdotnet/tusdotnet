using System;

namespace tusdotnet.Tus2
{
    public class TusHandlerLimits
    {
        public long? MaxSize { get; set; }

        public long? MinSize { get; set; }

        public long? MaxAppendSize { get; set; }

        public long? MinAppendSize { get; set; }

        public TimeSpan? Expiration { get; set; }
    }
}
