using System;
using Owin;

namespace Owin_net452_TestApp.Extensions
{
    public static class ExceptionHandlerIAppBuilderExtensions
    {
        /// <summary>
        /// Use a simple exception handler that will log errors and return 500 internal server error on exceptions.
        /// </summary>
        /// <param name="app"></param>
        public static void SetupSimpleExceptionHandler(this IAppBuilder app)
        {
            app.Use(async (context, next) =>
            {
                try
                {
                    await next.Invoke();
                }
                catch (Exception exc)
                {
                    Console.Error.WriteLine(exc);
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync("An internal server error has occurred");
                }
            });
        }
    }
}
