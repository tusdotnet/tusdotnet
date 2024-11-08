#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using System;
using System.Linq;
using System.Reflection;
using tusdotnet.ModelBinding.Models;

namespace tusdotnet.ModelBinding.ProtocolHandler
{
    internal static class ParameterDetector
    {
        internal static ResumableUploadParameterInfo GetParameterThatIsResumableUpload(
            this Endpoint endpoint
        )
        {
            if (endpoint is null)
                return null;

            var metadata = endpoint.Metadata.SingleOrDefault(x =>
                x is ControllerActionDescriptor || x is MethodInfo
            );

            if (metadata is null)
                return null;

            return metadata switch
            {
                ControllerActionDescriptor cad => ParseForMvc(cad),
                MethodInfo mi => ParseForMinimalApi(mi),
                _ => null
            };
        }

        private static ResumableUploadParameterInfo CreateResult(Type resumableUploadParameterType)
        {
            return new() { TypeOfResumableUploadParam = resumableUploadParameterType };
        }

        private static ResumableUploadParameterInfo ParseForMinimalApi(MethodInfo methodInfo)
        {
            var parameter = methodInfo
                .GetParameters()
                .SingleOrDefault(x => typeof(ResumableUpload).IsAssignableFrom(x.ParameterType));

            return CreateResult(parameter.ParameterType);
        }

        private static ResumableUploadParameterInfo ParseForMvc(
            ControllerActionDescriptor actionDescriptor
        )
        {
            var parameter = actionDescriptor
                .MethodInfo.GetParameters()
                .SingleOrDefault(x => typeof(ResumableUpload).IsAssignableFrom(x.ParameterType));

            if (parameter is null)
                return null;

            return CreateResult(parameter.ParameterType);
        }
    }
}
#endif
