using System;
using System.Threading;
using System.Windows.Threading;
using System.Xml;
using ParallelScan.Info;
using ParallelScan.InfoCollectors;
using ParallelScan.TaskProcessors;

namespace ParallelScan.TaskCoordinator
{
    class ScanCoordinator
    {
        public event Action<Exception> Failed = delegate { };
        public event Action Completed = delegate { };

        public event Action ItemScanned = delegate { };
        public event Action ItemWroteToFile = delegate { };
        public event Action ItemWroteToTree = delegate { };

        private readonly InfoCollector _infoCollector;
        private readonly FileWriterTaskProcessor _fileWriterTaskProcessor;
        private readonly TreeWriterInfoTaskProcessor _treeWriterInfoTaskProcessor;

        private bool _isStarted;

        private bool _isInfoCollectorFinished;
        private bool _isFileWriterProcessorFinished;
        private bool _isTreeWriterInfoTaskProcessorFinished;

        private SpinLock _lock; // can't be readonly

        public ScanCoordinator(string scanStartingPath, string scanResultsFilePath,
            XmlDocument scanResultsDocument, Dispatcher uiDispatcher)
        {
            _lock = new SpinLock();

            _infoCollector = new InfoCollector(scanStartingPath, new TaskInfoBuilder());
            _fileWriterTaskProcessor = new FileWriterTaskProcessor(scanResultsFilePath);
            _treeWriterInfoTaskProcessor = new TreeWriterInfoTaskProcessor(uiDispatcher, scanResultsDocument);

            _infoCollector.InfoCollected += OnItemScanned;
            _infoCollector.InfoCollected += _fileWriterTaskProcessor.QueueTask;
            _infoCollector.InfoCollected += _treeWriterInfoTaskProcessor.QueueTask;

            _fileWriterTaskProcessor.Processed += OnItemWroteToFile;
            _treeWriterInfoTaskProcessor.Processed += OnItemWroteToTree;

            _infoCollector.Completed += OnCollectingCompleted;
            _infoCollector.Completed += _fileWriterTaskProcessor.Finish;
            _infoCollector.Completed += _treeWriterInfoTaskProcessor.Finish;
            _fileWriterTaskProcessor.Completed += OnWritingToFileCompleted;
            _treeWriterInfoTaskProcessor.Completed += OnWritingToTreeCompleted;

            _infoCollector.Failed += OnFailed;
            _fileWriterTaskProcessor.Failed += OnFailed;
            _treeWriterInfoTaskProcessor.Failed += OnFailed;
        }
        
        public void Start()
        {
            if (_isStarted)
                throw new InvalidOperationException("Start operation can't be performed again.");

            _isStarted = true;
            _infoCollector.Start();
        }

        public void Cancel()
        {
            CancelWork();
        }

        #region Processing handlers

        private void OnItemScanned(TaskInfo info)
        {
            if (info.TaskType == TaskType.Add)
                ItemScanned();
        }

        private void OnItemWroteToFile(TaskInfo info)
        {
            if (info.TaskType == TaskType.Add)
                ItemWroteToFile();
        }

        private void OnItemWroteToTree(TaskInfo info)
        {
            if (info.TaskType == TaskType.Add)
                ItemWroteToTree();
        }

        #endregion

        #region Completion handlers

        private void OnCollectingCompleted()
        {
            _isInfoCollectorFinished = true;
        }

        private void OnWritingToFileCompleted()
        {
            if (!_isInfoCollectorFinished)
                throw new InvalidOperationException("Processor can not complete operation before Producer.");

            _isFileWriterProcessorFinished = true;

            TryComplete();
        }

        private void OnWritingToTreeCompleted()
        {
            if (!_isInfoCollectorFinished)
                throw new InvalidOperationException("Processor can not complete operation before Producer.");
                
            _isTreeWriterInfoTaskProcessorFinished = true;

            TryComplete();
        }

        private void TryComplete()
        {
            var lockTaken = false;
            _lock.Enter(ref lockTaken);

            if (_isFileWriterProcessorFinished && _isTreeWriterInfoTaskProcessorFinished)
                Completed();

            _lock.Exit();
        }

        #endregion

        private void OnFailed(Exception ex)
        {
            CancelWork();
            Failed(ex);
        }

        #region Helpers

        private void CancelWork()
        {
            if (!_isStarted)
                return;

            _infoCollector.Cancel();
            _fileWriterTaskProcessor.Cancel();
            _treeWriterInfoTaskProcessor.Cancel();
        }

        #endregion
    }
}
