using Xunit;

namespace tusdotnet.test
{
    internal class ConditionalFact : FactAttribute
    {
        public ConditionalFact(Conditions conditions)
        {
            if (conditions == Conditions.Events && !TestRunSettings.SupportsEvents)
                Skip = "Current tus runner does not support events";
        }
    }
}
