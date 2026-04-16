using System.Threading.Tasks;
using tusdotnet.Models;

namespace tusdotnet.Helpers
{
    internal static class TaskHelper
    {
#if netfull
        public static Task Completed { get; } = Task.FromResult(0);
#else
        public static Task Completed { get; } = Task.CompletedTask;
#endif

        public static Task<ResultType> ContinueExecution { get; } =
            Task.FromResult(ResultType.ContinueExecution);
    }
}
