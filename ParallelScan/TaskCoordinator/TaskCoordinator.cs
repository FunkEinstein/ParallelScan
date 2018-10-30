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
        private enum State
        {
            Created,
            Started,
            Completed,
            Failed,
            Canceled
        }

        private class ProcessorState<TArg>
        {
            public readonly ITaskProcessor<TArg> Processor;
            public bool IsCompleted;

            public ProcessorState(ITaskProcessor<TArg> processor)
            {
                Processor = processor;
            }
        }

        private bool _isProduceCompleted;
        private readonly ITaskProducer<TTaskInfo> _producer;

        private readonly List<ProcessorState<TTaskInfo>> _processorsStates;

        private State _state;

        private AutoResetEvent _event;

        public event Action<TTaskInfo> Produced = delegate { };
        public event Action<Exception> Failed = delegate { };
        public event Action Completed = delegate { };

        public TaskCoordinator(ITaskProducer<TTaskInfo> producer, params ITaskProcessor<TTaskInfo>[] processors)
        {
            _state = State.Created;

            _event = new AutoResetEvent(true);

            _producer = producer;
            _producer.Produced += i => Produced(i);

            _processorsStates = new List<ProcessorState<TTaskInfo>>(processors.Length);

            for (int i = 0; i < processors.Length; i++)
            {
                var processor = processors[i];
                _processorsStates.Add(new ProcessorState<TTaskInfo>(processor));

                producer.Produced += processor.QueueTask;

                producer.Completed += OnProduceCompleted;
                processor.Completed += () => OnProcessCompleted(processor);

                producer.Failed += OnFailed;
                processor.Failed += OnFailed;
            }
        }

        public void Start()
        {
            if (_state != State.Created)
                throw new InvalidOperationException("Start operation can't be performed again.");

            _state = State.Started;

            _producer.Start();
        }

        public void Cancel()
        {
            _state = State.Canceled;

            CancelWork();
        }

        private void OnProduceCompleted()
        {
            _isProduceCompleted = true;

            for (int i = 0; i < _processorsStates.Count; i++)
            {
                var processor = _processorsStates[i].Processor;
                processor.Finish();
            }
        }

        private void OnProcessCompleted(ITaskProcessor<TTaskInfo> processor)
        {
            if (!_isProduceCompleted)
                throw new InvalidOperationException("Processor can not complete operation before Producer.");

            var state = _processorsStates.First(st => st.Processor == processor);

            _event.WaitOne();

            state.IsCompleted = true;
            if (_isProduceCompleted && _processorsStates.All(st => st.IsCompleted))
            {
                _state = State.Completed;
                Completed();
            }

            _event.Set();
        }

        private void OnFailed(Exception ex)
        {
            _state = State.Failed;
            
            CancelWork();
            Failed(ex);
        }

        private void CancelWork()
        {
            _producer.Cancel();

            for (int i = 0; i < _processorsStates.Count; i++)
            {
                var processor = _processorsStates[i].Processor;
                processor.Cancel();
            }
        }
    }
}
