using System.Collections.Generic;

namespace ParallelScan.Info
{
    public enum TaskType : byte
    {
        Add = 0,
        Update
    }

    class TaskInfo
    {
        public List<Attribute> Attributes { get; set; }
        public string Name { get; set; }
        public bool IsDirectory { get; set; }
        public TaskType TaskType { get; set; }
    }
}
