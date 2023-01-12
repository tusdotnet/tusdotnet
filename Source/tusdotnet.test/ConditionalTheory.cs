using Xunit;

namespace tusdotnet.test
{
    internal class ConditionalTheory : TheoryAttribute
    {
        public ConditionalTheory(Conditions conditions)
        {
            if (conditions == Conditions.Events && !TestRunSettings.SupportsEvents)
                Skip = "Current tus runner does not support events";
        }
    }
}
