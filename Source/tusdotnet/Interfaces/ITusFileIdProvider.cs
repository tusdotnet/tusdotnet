using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
  /// <summary>
  /// Provides unique identifiers
  /// </summary>
  public interface ITusFileIdProvider
  {
    /// <summary>
    /// Gets/sets the fileId
    /// </summary>
    string FileId { get; }

    /// <summary>
    /// Sets the FileId to the value of fileId
    /// </summary>
    /// <param name="fileId"></param>
    ITusFileIdProvider Use(string fileId);
  }
}
