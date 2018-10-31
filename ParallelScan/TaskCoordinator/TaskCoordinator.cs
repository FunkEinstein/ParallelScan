using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ParallelScan.TaskProcessors;
using ParallelScan.TaskProducers;

namespace ParallelScan.TaskCoordinator
{
    class TaskCoordinator<TTaskInfo> : ITaskEvents
    {
        protected class ProducerStateInfo<TArg>
        {
            public readonly ITaskProducer<TArg> Producer;
            public bool IsCompleted;

            public ProducerStateInfo(ITaskProducer<TArg> producer)
            {
                Producer = producer;
            }
        }

        protected class ProcessorStateInfo<TArg>
        {
            public readonly ITaskProcessor<TArg> Processor;
            public bool IsCompleted;

            public ProcessorStateInfo(ITaskProcessor<TArg> processor)
            {
                Processor = processor;
            }
        }

        protected readonly ProducerStateInfo<TTaskInfo> ProducerState;
        protected readonly List<ProcessorStateInfo<TTaskInfo>> ProcessorStates;

        protected bool IsStarted;

        private AutoResetEvent _event;

        public event Action<Exception> Failed = delegate { };
        public event Action Completed = delegate { };

        public TaskCoordinator(ITaskProducer<TTaskInfo> producer, params ITaskProcessor<TTaskInfo>[] processors)
        {
            _event = new AutoResetEvent(true);

            ProducerState = new ProducerStateInfo<TTaskInfo>(producer);
            ProcessorStates = new List<ProcessorStateInfo<TTaskInfo>>(processors.Length);

            for (int i = 0; i < processors.Length; i++)
            {
                var processor = processors[i];
                ProcessorStates.Add(new ProcessorStateInfo<TTaskInfo>(processor));

                producer.Produced += processor.QueueTask;

                producer.Completed += OnProduceCompleted;
                processor.Completed += () => OnProcessCompleted(processor);

                producer.Failed += OnFailed;
                processor.Failed += OnFailed;
            }
        }

        public void Start()
        {
            if (IsStarted)
                throw new InvalidOperationException("Start operation can't be performed again.");

            IsStarted = true;
            ProducerState.Producer.Start();
        }

        public void Cancel()
        {
            CancelWork();
        }

        private void OnProduceCompleted()
        {
            ProducerState.IsCompleted = true;

            for (int i = 0; i < ProcessorStates.Count; i++)
            {
                var processor = ProcessorStates[i].Processor;
                processor.Finish();
            }
        }

        private void OnProcessCompleted(ITaskProcessor<TTaskInfo> processor)
        {
            if (!ProducerState.IsCompleted)
                throw new InvalidOperationException("Processor can not complete operation before Producer.");

            var state = ProcessorStates.First(st => st.Processor == processor);

            _event.WaitOne();

            state.IsCompleted = true;
            if (ProcessorStates.All(st => st.IsCompleted))
                Completed();

            _event.Set();
        }

        private void OnFailed(Exception ex)
        {
            CancelWork();
            Failed(ex);
        }

        private void CancelWork()
        {
            if (!IsStarted)
                return;

            ProducerState.Producer.Cancel();

            for (int i = 0; i < ProcessorStates.Count; i++)
            {
                var processor = ProcessorStates[i].Processor;
                processor.Cancel();
            }
        }
    }
}