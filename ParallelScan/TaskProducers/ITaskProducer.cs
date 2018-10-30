using System;
using ParallelScan.TaskCoordinator;

namespace ParallelScan.TaskProducers
{
    interface ITaskProducer<TTaskInfo> : ITaskEvents
    {
        event Action<TTaskInfo> Produced;

        void Start();
        void Cancel();
    }
}
