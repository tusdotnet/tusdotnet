using System.IO.Pipelines;

namespace tusdotnet.Tus2
{
    public class WriteDataContext : Tus2Context
    {
        public PipeReader BodyReader { get; set; }
    }
}
