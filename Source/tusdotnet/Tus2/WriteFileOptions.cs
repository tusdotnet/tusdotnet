using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tusdotnet.Models;

namespace tusdotnet.Tus2
{
    internal class WriteFileOptions
    {
        public IDictionary<string, Metadata> Metadata { get; set; }
    }
}
