using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AspNetCore_netcoreapp3_0_TestApp.Middleware
{
    public class SimpleExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SimpleExceptionHandlerMiddleware> _logger;

        public SimpleExceptionHandlerMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<SimpleExceptionHandlerMiddleware>();
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next.Invoke(context);
            }
            catch (Exception exc)
            {
                _logger.LogError(exc, exc.Message);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("An internal server error has occurred", context.RequestAborted);
            }
        }
    }

    public static class SimpleExceptionHandlerMiddlewareExtensions
    {
        /// <summary>
        /// Use a simple exception handler that will log errors and return 500 internal server error on exceptions.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseSimpleExceptionHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SimpleExceptionHandlerMiddleware>();
        }
    }
}
