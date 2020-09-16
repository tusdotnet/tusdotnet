namespace tusdotnet.Models.Configuration
{
    public class ResolveClientTagContext : EventContext<ResolveClientTagContext>
    {
        public string UploadTag { get; internal set; }

        public bool ClientTagBelongsToCurrentUser { get; internal set; }

        public bool RequestPassesChallenge { get; internal set; }

        internal bool RequestIsAllowed { get; private set; }

        public void Allow()
        {
            RequestIsAllowed = true;
        }
    }
}
