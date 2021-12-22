using System.Collections.Generic;
using tusdotnet.Models;

namespace tusdotnet.Tus2
{
    public class CreateFileContext
    {
        public Dictionary<string, Metadata> Metadata { get; set; }
    }
}
