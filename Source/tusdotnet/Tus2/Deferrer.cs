using System;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal class Deferrer : IAsyncDisposable
    {
        private readonly Func<Task> _cleanup;

        private Deferrer(Func<Task> cleanup)
        {
            _cleanup = cleanup;
        }

        public async ValueTask DisposeAsync()
        {
            await _cleanup();
        }

        public static Deferrer Defer(Func<Task> action) => new Deferrer(action);
    }
}
