using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ParallelScan.Info;

namespace ParallelScan.TaskProcessors
{
    abstract class FileInfoTaskProcessor : ITaskProcessor<FileTaskInfo>
    {
        private readonly object _tasksSync = new object();
        private readonly Queue<FileTaskInfo> _tasks;

        private Task _task;
        private readonly CancellationTokenSource _cts;
        private readonly SemaphoreSlim _semaphore;

        private bool _isProviderComplete;

        protected bool IsSuccessfullyCompleted
        {
            get { return _isProviderComplete && _tasks.Count == 0; }
        }

        public abstract event Action<FileTaskInfo> Processed;
        public event Action<Exception> Failed = delegate { };
        public event Action Completed = delegate { };

        protected FileInfoTaskProcessor()
        {
            _tasks = new Queue<FileTaskInfo>();
            _semaphore = new SemaphoreSlim(0);
            _cts = new CancellationTokenSource();

            _isProviderComplete = false;
        }
        
        #region Handlers

        public void QueueTask(FileTaskInfo info)
        {
            Monitor.Enter(_tasksSync);
            _tasks.Enqueue(info);
            Monitor.Exit(_tasksSync);

            _semaphore.Release();

            if (_task == null)
                _task = Task.Factory.StartNew(ProcessTask, _cts.Token);
        }

        public void Finish()
        {
            _isProviderComplete = true;
            _semaphore.Release();
        }

        public void Cancel()
        {
            _cts.Cancel();
            _semaphore.Release();
        }

        #endregion

        #region Processors

        protected async void ProcessTask()
        {
            try
            {
                while (!IsSuccessfullyCompleted)
                {
                    await _semaphore.WaitAsync();

                    _cts.Token.ThrowIfCancellationRequested();

                    if (IsSuccessfullyCompleted)
                        break;

                    Monitor.Enter(_tasksSync);
                    var info = _tasks.Dequeue();
                    Monitor.Exit(_tasksSync);

                    ProcessInfo(info);
                }

                Completed();
            }
            catch (OperationCanceledException) { } // Swallow that
            catch (Exception ex)
            {
                Failed(ex);
            }
            finally
            {
                if (Monitor.IsEntered(_tasks))
                    Monitor.Exit(_tasks);

                FinalizeProcessing();
            }
        }

        protected abstract void ProcessInfo(FileTaskInfo info);

        protected virtual void FinalizeProcessing()
        { }

        #endregion
    }
}
