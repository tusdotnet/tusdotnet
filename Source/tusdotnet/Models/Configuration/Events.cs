using System;
using System.Threading.Tasks;

namespace tusdotnet.Models.Configuration
{
    /// <summary>
    /// Events supported by tusdotnet
    /// </summary>
    public class Events
    {
        /// <summary>
        /// Callback that will run right before writing the data has started.
        /// This event can fire multiple times as the client can split the upload into multiple requests.
        /// This callback can be used to perform additional validations or preparations before the upload process begins.
        /// Calling the <see cref="ValidationContext{TSelf}.FailRequest(string)" /> method on the context will return a 400 Bad Request to the client.
        /// </summary>
        public Func<BeforeWriteContext, Task> OnBeforeWriteAsync { get; set; }

        /// <summary>
        /// Callback that will run when a file is completely uploaded.
        /// This callback is called only once after the last bytes have been written to the store or
        /// after a "final" file has been created using the concatenation extension.
        /// It will not be called for any subsequent requests for already completed files.
        /// </summary>
        public Func<FileCompleteContext, Task> OnFileCompleteAsync { get; set; }

        /// <summary>
        /// Callback that will run right before a file is created. This callback can be used to validate
        /// files before they are created. Calling the <see cref="ValidationContext{TSelf}.FailRequest(string)" /> method on the context
        /// will return a 400 Bad Request to the client.
        /// </summary>
        public Func<BeforeCreateContext, Task> OnBeforeCreateAsync { get; set; }

        /// <summary>
        /// Callback that will run when a file has been created.
        /// </summary>
        public Func<CreateCompleteContext, Task> OnCreateCompleteAsync { get; set; }

        /// <summary>
        /// Callback that will run right before a file is deleted. This callback can be used to validate
        /// that a file can be deleted. Calling the <see cref="ValidationContext{TSelf}.FailRequest(string)" /> method on the context
        /// will return a 400 Bad Request to the client.
        /// </summary>
        public Func<BeforeDeleteContext, Task> OnBeforeDeleteAsync { get; set; }

        /// <summary>
        /// Callback that will run when a file has been deleted.
        /// </summary>
        public Func<DeleteCompleteContext, Task> OnDeleteCompleteAsync { get; set; }

        /// <summary>
        /// Callback that will run before the request is being handled. This callback can be used to authorize the current request.
        /// Calling the <see cref="ValidationContext{TSelf}.FailRequest(string)" /> method on the context will return a 400 Bad Request to the client.
        /// </summary>
        public Func<AuthorizeContext, Task> OnAuthorizeAsync { get; set; }
    }
}
