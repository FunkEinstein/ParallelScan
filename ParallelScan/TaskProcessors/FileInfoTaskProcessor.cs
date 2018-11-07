using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ParallelScan.Info;

namespace ParallelScan.TaskProcessors
{
    abstract class FileInfoTaskProcessor
    {
        public abstract event Action<TaskInfo> Processed;
        public event Action<Exception> Failed = delegate { };
        public event Action Completed = delegate { };

        private readonly Queue<TaskInfo> _tasks;

        private Task _task;
        private readonly CancellationTokenSource _cts;
        private readonly SemaphoreSlim _semaphore;
        private SpinLock _spinLock; // can't be readonly

        private bool _isProviderComplete;

        protected bool IsSuccessfullyCompleted
        {
            get { return _isProviderComplete && _tasks.Count == 0; }
        }

        protected FileInfoTaskProcessor()
        {
            _tasks = new Queue<TaskInfo>();
            _semaphore = new SemaphoreSlim(0);
            _spinLock = new SpinLock();
            _cts = new CancellationTokenSource();

            _isProviderComplete = false;
        }
        
        #region Handlers

        public void QueueTask(TaskInfo info)
        {
            var lockTaken = false;
            _spinLock.Enter(ref lockTaken);
            _tasks.Enqueue(info);
            _spinLock.Exit();

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
            var lockTaken = false;

            try
            {
                while (!IsSuccessfullyCompleted)
                {
                    await _semaphore.WaitAsync();

                    _cts.Token.ThrowIfCancellationRequested();

                    if (IsSuccessfullyCompleted)
                        break;

                    _spinLock.Enter(ref lockTaken);
                    var info = _tasks.Dequeue();
                    _spinLock.Exit();
                    lockTaken = false;

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
                if (lockTaken)
                    _spinLock.Exit();

                FinalizeProcessing();
            }
        }

        protected abstract void ProcessInfo(TaskInfo info);

        protected virtual void FinalizeProcessing()
        { }

        #endregion
    }
}
