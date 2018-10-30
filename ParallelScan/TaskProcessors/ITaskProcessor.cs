using System;
using ParallelScan.TaskCoordinator;

namespace ParallelScan.TaskProcessors
{
    interface ITaskProcessor<TTaskInfo> : ITaskEvents
    {
        event EventHandler<TTaskInfo> Processed;

        void QueueTask(TTaskInfo info);
        void Finish();
        void Cancel();
    }
}
