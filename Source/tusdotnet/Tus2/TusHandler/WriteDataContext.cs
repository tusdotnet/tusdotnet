using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public class WriteDataContext : Tus2Context
    {
        public PipeReader BodyReader { get; set; }

        public Func<long, Task> ReportOffset { get; set; }
    }
}
