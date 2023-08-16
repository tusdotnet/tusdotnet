#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using System;
using System.Linq;
using System.Reflection;
using tusdotnet.ModelBinders.Validation;

namespace tusdotnet.ModelBinders
{
    internal static class ParameterDetector
    {
        internal static ResumableUploadParameterInfo GetParameterThatIsResumableUpload(this Endpoint endpoint, IServiceProvider services, bool bindAnyType)
        {
            if (endpoint is null)
                return null;

            var metadata = endpoint.Metadata.SingleOrDefault(x => x is ControllerActionDescriptor || x is MethodInfo);

            if (metadata is null)
                return null;

            return metadata switch
            {
                ControllerActionDescriptor cad when bindAnyType is false => ParseForMvc(cad, services),
                ControllerActionDescriptor cad when bindAnyType is true => ParseForMvcUsingFromBody(cad, services),
                MethodInfo mi when bindAnyType is false => ParseForMinimalApi(mi, services),
                MethodInfo mi when bindAnyType is true => ParseForMinimalApiUsingFromBody(mi, services),
                _ => null
            };
        }

        private static ResumableUploadParameterInfo CreateResult(Type resumableUploadParameterType, IServiceProvider services)
        {
            return new()
            {
                //MetadataValidator = validatorType is not null ? (MetadataValidator)services.GetRequiredService(validatorType) : null
                TypeOfResumableUploadParam = resumableUploadParameterType
            };
        }

        //private static Type GetValidatorFromAttribute(ParameterInfo parameter)
        //{
        //    var attr = parameter.GetCustomAttributes().FirstOrDefault(x => x.GetType().IsGenericType && x.GetType().GetGenericTypeDefinition() == typeof(ValidateUsingAttribute<>));
        //    if (attr is null)
        //        return null;

        //    return attr.GetType().GenericTypeArguments[0];
        //}

        private static ResumableUploadParameterInfo ParseForMinimalApi(MethodInfo methodInfo, IServiceProvider services)
        {
            var parameter = methodInfo.GetParameters().SingleOrDefault(x => typeof(ResumableUpload).IsAssignableFrom(x.ParameterType));
            //var validatorType = GetValidatorFromAttribute(parameter);
            return CreateResult(parameter.ParameterType, services);
        }

        private static ResumableUploadParameterInfo ParseForMinimalApiUsingFromBody(MethodInfo methodInfo, IServiceProvider services)
        {
            //var parameter = methodInfo.GetParameters().SingleOrDefault(x => x.GetCustomAttribute(typeof(FromBodyAttribute)) is not null);
            var parameter = methodInfo.GetParameters().SingleOrDefault(x => x.ParameterType.Name == "MyClass");
            //var validatorType = GetValidatorFromAttribute(parameter);
            return CreateResult(parameter.ParameterType, services);
        }

        private static ResumableUploadParameterInfo ParseForMvc(ControllerActionDescriptor actionDescriptor, IServiceProvider services)
        {
            var parameter = actionDescriptor.MethodInfo.GetParameters().SingleOrDefault(x => typeof(ResumableUpload).IsAssignableFrom(x.ParameterType));
            //var validatorType = GetValidatorFromAttribute(paramDescriptor);
            return CreateResult(parameter.ParameterType, services);
        }

        private static ResumableUploadParameterInfo ParseForMvcUsingFromBody(ControllerActionDescriptor actionDescriptor, IServiceProvider services)
        {
            var parameter = actionDescriptor.MethodInfo.GetParameters().SingleOrDefault(x => x.GetCustomAttribute(typeof(FromBodyAttribute)) is not null);
            return CreateResult(parameter.ParameterType, services);
        }
    }
}
#endif