using ParallelScan.TaskProducers;
using System;

namespace ParallelScan.TaskProcessors
{
    interface ITaskProcessor<TArg>
    {
        event EventHandler<TArg> Processed;
        event EventHandler<Exception> Failed;
        event EventHandler Canceled;
        event EventHandler Completed;

        void Subscribe(ITaskProducer<TArg> producer);

        void OnReceiveTask(object sender, TArg info);
        void OnCanceled(object sender, EventArgs args);
        void OnFailed(object sender, Exception args);
        void OnProviderComplete(object sender, EventArgs args);
    }
}
