namespace tusdotnet.Tus2
{
    internal class NamedFactory<T>
    {
        public string Name { get; set; }

        public T Factory { get; set; }
    }
}
