using System;

namespace ParallelScan.TaskProducers
{
    interface ITaskProducer<TArg>
    {
        event EventHandler<TArg> Produced;
        event EventHandler<Exception> Failed;
        event EventHandler Canceled;
        event EventHandler Completed;

        void OnCanceled(object sender, EventArgs args);
        void OnFailed(object sender, Exception args);
    }
}
