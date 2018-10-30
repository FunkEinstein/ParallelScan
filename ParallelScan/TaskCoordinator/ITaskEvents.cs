using System;

namespace ParallelScan.TaskCoordinator
{
    interface ITaskEvents
    {
        event Action<Exception> Failed;
        event Action Completed;
    }
}
