using System;
using System.Windows.Threading;
using System.Xml;
using ParallelScan.Info;
using ParallelScan.TaskProcessors;
using ParallelScan.TaskProducers;

namespace ParallelScan.TaskCoordinator
{
    class ScanCoordinator : TaskCoordinator<FileTaskInfo>
    {
        public event Action ItemScanned = delegate { };
        public event Action ItemWroteToFile = delegate { };
        public event Action ItemWroteToXml = delegate { };

        public ScanCoordinator(string scanStartingPath, string scanResultsFilePath,
            XmlDocument scanResultsDocument, Dispatcher uiDispatcher)
            : base(new FileInfoTaskProducer(scanStartingPath),
                new FileWriterTaskProcessor(scanResultsFilePath),
                new TreeWriterInfoTaskProcessor(uiDispatcher, scanResultsDocument))
        {
            ProducerState.Producer.Produced += OnItemScanned;

            for (int i = 0; i < ProcessorStates.Count; i++)
            {
                var processor = ProcessorStates[i].Processor;

                if (processor is FileWriterTaskProcessor)
                    processor.Processed += OnItemWroteToFile;
                else
                    processor.Processed += OnItemWroteToXml;
            }
        }

        private void OnItemScanned(FileTaskInfo info)
        {
            if (info.TaskType == TaskType.Add)
                ItemScanned();
        }

        private void OnItemWroteToFile(FileTaskInfo info)
        {
            if (info.TaskType == TaskType.Add)
                ItemWroteToFile();
        }

        private void OnItemWroteToXml(FileTaskInfo info)
        {
            if (info.TaskType == TaskType.Add)
                ItemWroteToXml();
        }

    }
}
