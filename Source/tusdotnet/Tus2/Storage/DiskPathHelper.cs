using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal class DiskPathHelper
    {
        private readonly string _diskPath;

        public DiskPathHelper(string diskPath)
        {
            _diskPath = diskPath;
        }

        internal string DataFilePath(string uploadToken)
        {
            return Path.Combine(_diskPath, uploadToken);
        }

        internal string MetadataFilePath(string uploadToken)
        {
            return Path.Combine(_diskPath, uploadToken) + ".metadata";
        }

        internal string CompletedFilePath(string uploadToken)
        {
            return Path.Combine(_diskPath, uploadToken) + ".completed";
        }

        internal string OngoingFilePath(string uploadToken)
        {
            return Path.Combine(_diskPath, uploadToken) + ".ongoing";
        }

        internal string CancelFilePath(string uploadToken)
        {
            return Path.Combine(_diskPath, uploadToken) + ".cancel";
        }
    }
}
