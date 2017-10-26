using System;
using System.Threading.Tasks;

namespace tusdotnet.Models.Configuration
{
    /// <summary>
    /// Events supported by tusdotnet
    /// </summary>
    public class Events
    {
        /// <summary>
        /// Callback ran when a file is completely uploaded. 
        /// This callback is called only once after the last bytes have been written to the store or 
        /// after a "final" file has been created using the concatenation extension.
        /// It will not be called for any subsequent requests for already completed files.
        /// </summary>
        public Func<FileCompleteContext, Task> OnFileCompleteAsync { get; set; }
    }
}
