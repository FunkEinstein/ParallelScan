using ParallelScan.Info;
using ParallelScan.TaskProducers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelScan.TaskProcessors
{
    class FileWriterTaskProcessor : ITaskProcessor<TaskInfo>, IDisposable
    {
        private const char _tab = '\t';
        private const int _size = 12;

        private readonly object _tasksSync = new object();
        private readonly Queue<TaskInfo> _tasks;

        private Task _task;
        private readonly CancellationTokenSource _cts;
        private readonly AutoResetEvent _event;

        private bool _isProviderComplete;

        private readonly Stack<long> _offset;

        private FileStream _file;
        private StreamWriter _writer;
        readonly string _filePath;

        private bool _isDisposed;

        public event EventHandler<TaskInfo> Processed = delegate { };
        public event EventHandler<Exception> Failed = delegate { };
        public event EventHandler Canceled = delegate { };
        public event EventHandler Completed = delegate { };


        public FileWriterTaskProcessor(string path, ITaskProducer<TaskInfo> producer)
        {
            Subscribe(producer);

            _tasks = new Queue<TaskInfo>();
            _offset = new Stack<long>();

            _event = new AutoResetEvent(false);
            _cts = new CancellationTokenSource();

            _isProviderComplete = false;
            _filePath = path;
        }


        public void Subscribe(ITaskProducer<TaskInfo> producer)
        {
            producer.Produced += OnReceiveTask;
            producer.Canceled += OnCanceled;
            producer.Failed += OnFailed;
            producer.Completed += OnProviderComplete;
        }


        public void OnCanceled(object sender, EventArgs args)
        {
            _cts.Cancel();
            _event.Set();
        }

        public void OnFailed(object sender, Exception args)
        {
            _cts.Cancel();
            _event.Set();
        }

        public void OnProviderComplete(object sender, EventArgs args)
        {
            _isProviderComplete = true;
            _event.Set();
        }


        public void OnReceiveTask(object sender, TaskInfo info)
        {
            Monitor.Enter(_tasksSync);
            _tasks.Enqueue(info);
            Monitor.Exit(_tasksSync);

            _event.Set();

            if (_task == null)
            {
                _file = new FileStream(_filePath, FileMode.Create, FileAccess.Write);
                _writer = new StreamWriter(_file);

                _task = Task.Factory.StartNew(ProcessTask, _cts.Token);
            }
        }



        private void ProcessTask()
        {
            try
            {
                while (!(_isProviderComplete && _tasks.Count == 0))
                {
                    _event.WaitOne();

                    if (_tasks.Count == 0)
                        continue;

                    Monitor.Enter(_tasksSync);
                    var info = _tasks.Dequeue();
                    Monitor.Exit(_tasksSync);

                    if (info.TaskType == TaskType.Update)
                        Update(info);
                    else
                        Write(info);

                    _cts.Token.ThrowIfCancellationRequested();

                    if (_tasks.Count != 0)
                        _event.Set();
                }

                Dispose();

                Completed(this, null);
            }
            catch (OperationCanceledException)
            {
                Dispose();

                if (File.Exists(_filePath))
                    File.Delete(_filePath);

                Canceled(this, null);
            }
            catch (Exception ex)
            {
                Dispose();

                if (File.Exists(_filePath))
                    File.Delete(_filePath);

                Failed(this, ex);
            }
            finally
            {
                Dispose();

                if (Monitor.IsEntered(_tasks))
                    Monitor.Exit(_tasks);
            }
        }


        private void Write(TaskInfo info)
        {
            _cts.Token.ThrowIfCancellationRequested();

            var builder = new StringBuilder(256);

            var tabs = new string(_tab, _offset.Count);

            var nodeType = info.IsDirectory ? "dir" : "file";

            builder.AppendFormat("{0}<", tabs);
            builder.Append(nodeType);

            builder.AppendFormat(" Name=\"{0}\"", info.Name);

            foreach (var attr in info.Attributes)
                builder.AppendFormat(" {0}=\"{1}\"", attr.Name, attr.Value);

            if (info.IsDirectory)
            {
                builder.AppendFormat(" {0}=\"", "Size");

                _writer.Write(builder.ToString());
                _writer.Flush();

                _offset.Push(_writer.BaseStream.Position);
                builder.Clear();

                builder.AppendFormat("{0}\"", new string('0', _size));

                builder.Append(" >");
            }
            else
                builder.Append(" />");

            _writer.WriteLine(builder.ToString());

            Processed(this, info);
        }

        private void Update(TaskInfo info)
        {
            _cts.Token.ThrowIfCancellationRequested();

            var currentOffset = _offset.Pop();

            _writer.WriteLine("{0}</dir>", new string(_tab, _offset.Count));
            _writer.Flush();

            var size = info.Attributes.FirstOrDefault(at => at.Name == "Size");
            if (size == null)
                return;

            var offset = currentOffset + _size - size.Value.Length;
            var currentPosition = _writer.BaseStream.Position;

            _file.Seek(offset, SeekOrigin.Begin);
            _writer = new StreamWriter(_file);

            _writer.Write(size.Value);
            _writer.Flush();

            _file.Seek(currentPosition, SeekOrigin.Begin);
            _writer = new StreamWriter(_file);
        }


        public void Dispose()
        {
            if (_isDisposed)
                return;

            _writer.Flush();

            _writer.Dispose();
            _file.Dispose();

            _isDisposed = true;
        }
    }
}
