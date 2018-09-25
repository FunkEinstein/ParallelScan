using ParallelScan.Info;
using ParallelScan.TaskProducers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Xml;

namespace ParallelScan.TaskProcessors
{
    class TreeWriterTaskProcessor : ITaskProcessor<TaskInfo>
    {
        private readonly object _tasksSync = new object();
        private readonly Queue<TaskInfo> _tasks;

        private Task _task;
        private readonly CancellationTokenSource _cts;
        private readonly AutoResetEvent _event;

        private bool _isProviderComplete;

        private readonly Stack<XmlNode> _nodes;
        private XmlNode _currentNode;

        private readonly Dispatcher _dispatcher;
        private readonly XmlDocument _document;

        public event EventHandler<TaskInfo> Processed = delegate { };
        public event EventHandler<Exception> Failed = delegate { };
        public event EventHandler Canceled = delegate { };
        public event EventHandler Completed = delegate { };


        public TreeWriterTaskProcessor(Dispatcher dispatcher, XmlDocument document, ITaskProducer<TaskInfo> producer)
        {
            Subscribe(producer);

            _dispatcher = dispatcher;
            _document = document;

            _tasks = new Queue<TaskInfo>();
            _nodes = new Stack<XmlNode>();

            _event = new AutoResetEvent(false);
            _cts = new CancellationTokenSource();

            _isProviderComplete = false;
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
            Monitor.Enter(_tasks);
            _tasks.Enqueue(info);
            Monitor.Exit(_tasks);

            _event.Set();

            if (_task == null)
                _task = Task.Factory.StartNew(Set, _cts.Token);
        }


        private void Set()
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

                    Monitor.Enter(_document);
                    _dispatcher.Invoke(
                    () =>
                    {
                        if (_currentNode == null)
                        {
                            _currentNode = _document.DocumentElement;
                            EditElement(info);
                            return;
                        }

                        if (info.TaskType == TaskType.Update)
                            EditElement(info);
                        else
                            CreateElement(info);

                    });
                    Monitor.Exit(_document);

                    _cts.Token.ThrowIfCancellationRequested();

                    if (_tasks.Count != 0)
                        _event.Set();
                }

                Completed(this, null);
            }
            catch (OperationCanceledException)
            {
                Canceled(this, null);
            }
            catch (Exception ex)
            {
                Failed(this, ex);
            }
            finally
            {
                if (Monitor.IsEntered(_tasks))
                    Monitor.Exit(_tasks);
                if (Monitor.IsEntered(_document))
                    Monitor.Exit(_document);
            }
        }


        private void CreateElement(TaskInfo info)
        {
            var nodeType = info.IsDirectory ? "dir" : "file";

            var child = _document.CreateElement(nodeType);
            var attr = _document.CreateAttribute("Name");
            attr.InnerText = info.Name;
            child.Attributes.Append(attr);

            foreach (var attribute in info.Attributes)
            {
                attr = _document.CreateAttribute(attribute.Name);
                attr.InnerText = attribute.Value;
                child.Attributes.Append(attr);
            }

            _currentNode.AppendChild(child);

            if (info.IsDirectory)
            {
                _nodes.Push(_currentNode);
                _currentNode = child;
            }

            Processed(this, info);
        }

        private void EditElement(TaskInfo info)
        {
            if (_currentNode.Attributes.Count == 1)
            {
                foreach (var at in info.Attributes)
                {
                    var attr = _document.CreateAttribute(at.Name);
                    attr.InnerText = at.Value;
                    _currentNode.Attributes.Append(attr);
                }

                Processed(this, info);

                return;
            }

            var size = info.Attributes.FirstOrDefault(at => at.Name == "Size");

            if (size != null)
            {
                var attr = _document.CreateAttribute(size.Name);
                attr.InnerText = size.Value;

                _currentNode.Attributes.Append(attr);
            }

            if (_nodes.Count != 0)
                _currentNode = _nodes.Pop();
        }
    }
}
