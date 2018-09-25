
namespace ParallelScan.Info
{
    class Attribute
    {
        public string Name { get; set; }
        public string Value { get; set; }


        public Attribute()
        {
 
        }

        public Attribute(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }
}
