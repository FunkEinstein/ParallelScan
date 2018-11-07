namespace ParallelScan.Info
{
    struct InfoAttribute
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public InfoAttribute(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public bool IsDefault()
        {
            return Name == null && Value == null;
        }
    }
}
