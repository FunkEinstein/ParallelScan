using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ParallelScan.Info;

namespace ParallelScan.InfoCollectors
{
    class InfoCollector
    {
        private Task _task;
        private readonly CancellationTokenSource _cts;

        private readonly TaskInfoBuilder _infoBuilder;
        private readonly string _baseDirectoryPath;

        public event Action<TaskInfo> InfoCollected = delegate { };
        public event Action<Exception> Failed = delegate { };
        public event Action Completed = delegate { };

        public InfoCollector(string baseDirectoryPath, TaskInfoBuilder builder)
        {
            _infoBuilder = builder;
            _cts = new CancellationTokenSource();
            _baseDirectoryPath = baseDirectoryPath;
        }

        public void Start()
        {
            if (_task == null)
                _task = Task.Factory.StartNew(Produce, _cts.Token);
        }

        public void Cancel()
        {
            _cts.Cancel();
        }

        #region Produce

        private void Produce()
        {
            try
            {
                GetStructure(_baseDirectoryPath);

                Completed();
            }
            catch (OperationCanceledException) { } // Swallow this
            catch (Exception ex)
            {
                Failed(ex);
            }
        }

        public void GetStructure(string path)
        {
            var info = new DirectoryInfo(path);

            InfoCollected(_infoBuilder.Build(info));

            GetStructure(info);
        }

        private long GetStructure(DirectoryInfo info)
        {
            _cts.Token.ThrowIfCancellationRequested();

            var size = 0L;

            DirectoryInfo[] dirs;
            try
            {
                dirs = info.GetDirectories();
            }
            catch (UnauthorizedAccessException) // swallow this exception
            {
                return size;
            }

            for (int i = 0; i < dirs.Count(); i++)
            {
                InfoCollected(_infoBuilder.Build(dirs[i]));

                size += GetStructure(dirs[i]);
            }

            var files = info.GetFiles();
            for (int i = 0; i < files.Count(); i++)
            {
                InfoCollected(_infoBuilder.Build(files[i]));

                size += files[i].Length;
            }

            InfoCollected(_infoBuilder.Build(info, size));

            return size;
        }

        #endregion
    }
}
