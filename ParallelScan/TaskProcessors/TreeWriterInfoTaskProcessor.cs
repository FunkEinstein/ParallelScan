﻿using ParallelScan.Info;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using System.Xml;

namespace ParallelScan.TaskProcessors
{
    class TreeWriterInfoTaskProcessor : FileInfoTaskProcessor
    {
        public override event Action<TaskInfo> Processed = delegate { };

        private readonly Stack<XmlNode> _nodes;
        private XmlNode _currentNode;

        private readonly Dispatcher _dispatcher;
        private readonly XmlDocument _document;

        public TreeWriterInfoTaskProcessor(Dispatcher dispatcher, XmlDocument document)
        {
            _dispatcher = dispatcher;
            _document = document;

            _nodes = new Stack<XmlNode>();
        }

        #region Process

        protected override void ProcessInfo(TaskInfo info)
        {
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
        }

        protected override void FinalizeProcessing()
        {
            if (Monitor.IsEntered(_document))
                Monitor.Exit(_document);
        }
        
        #endregion
        
        #region Helpers

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

            Processed(info);
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

                Processed(info);

                return;
            }

            var size = info.Attributes.FirstOrDefault(at => at.Name == "Size");
            if (!size.IsDefault())
            {
                var attr = _document.CreateAttribute(size.Name);
                attr.InnerText = size.Value;

                _currentNode.Attributes.Append(attr);
            }

            if (_nodes.Count != 0)
                _currentNode = _nodes.Pop();
        }

        #endregion
    }
}
