using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
    /// <summary>
    /// Interface to implement a custom file id provider for example using a database
    /// </summary>
    public interface ITusFileIdProvider
    {
        /// <summary>
        /// Creates a new file id
        /// </summary>
        string CreateId();

        /// <summary>
        /// Checks if the file id is valid
        /// </summary>
        bool ValidateId(string fileId);
    }
}
