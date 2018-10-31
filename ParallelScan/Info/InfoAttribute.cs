namespace ParallelScan.Info
{
    class InfoAttribute
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public InfoAttribute(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }
}
