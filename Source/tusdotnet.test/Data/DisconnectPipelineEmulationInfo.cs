using System;

namespace tusdotnet.test.Data
{
    internal sealed class DisconnectPipelineEmulationInfo
    {
        public bool FlagsCancellationTokenAsCancelled { get; set; }

        public Exception ExceptionThatIsThrown { get; set; }

        public DisconnectPipelineEmulationInfo(bool flagsCancellationTokenAsCancelled, Exception exceptionThatIsThrown)
        {
            FlagsCancellationTokenAsCancelled = flagsCancellationTokenAsCancelled;
            ExceptionThatIsThrown = exceptionThatIsThrown;
        }
    }
}