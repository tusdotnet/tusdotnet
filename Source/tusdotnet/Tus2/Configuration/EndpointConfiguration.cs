using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public class EndpointConfiguration
    {
        public string StorageConfigurationName { get; set; }

        public string UploadManagerConfigurationName { get; set; }

        public bool? AllowClientToDeleteFile { get; set; }

        public EndpointConfiguration(string configurationName)
            : this(configurationName, configurationName)
        {
        }

        public EndpointConfiguration(string storageConfigurationName, string uploadManagerConfigurationName)
        {
            StorageConfigurationName = storageConfigurationName;
            UploadManagerConfigurationName = uploadManagerConfigurationName;
        }
    }
}
