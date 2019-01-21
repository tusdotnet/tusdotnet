using System.Threading.Tasks;

namespace tusdotnet.Helpers
{
    internal static class TaskHelper
    {
        public static Task Completed { get; } = Task.FromResult(0);
    }
}
