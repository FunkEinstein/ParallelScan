using System.Collections.Generic;

namespace ParallelScan.Info
{
    struct TaskInfo
    {
        public List<InfoAttribute> Attributes { get; set; }
        public string Name { get; set; }
        public bool IsDirectory { get; set; }
        public TaskType TaskType { get; set; }
    }
}
