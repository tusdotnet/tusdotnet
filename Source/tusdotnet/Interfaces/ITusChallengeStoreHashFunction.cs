namespace tusdotnet.Interfaces
{
    internal interface ITusChallengeStoreHashFunction
    {
        byte[] ComputeHash(string input);
    }
}
