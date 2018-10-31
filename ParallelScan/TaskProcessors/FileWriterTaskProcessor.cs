using ParallelScan.Info;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ParallelScan.TaskProcessors
{
    class FileWriterTaskProcessor: FileInfoTaskProcessor, IDisposable
    {
        private const char Tab = '\t';
        private const int Size = 12;

        private readonly Stack<long> _offset;

        private readonly string _filePath;
        private readonly FileStream _file;

        private StreamWriter _writer;

        public override event Action<FileTaskInfo> Processed = delegate { };

        public FileWriterTaskProcessor(string path) 
        {
            _offset = new Stack<long>();
            _filePath = path;
            _file = new FileStream(_filePath, FileMode.Create, FileAccess.Write);
            _writer = new StreamWriter(_file);
        }

        #region Processors

        protected override void ProcessInfo(FileTaskInfo info)
        {
            if (info.TaskType == TaskType.Update)
                UpdateInfo(info);
            else
                WriteInfo(info);
        }

        protected override void FinalizeProcessing()
        {
            Dispose();

            if (!IsSuccessfullyCompleted)
                DeleteFile();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _writer.Flush();
            _writer.Dispose();

            _file.Dispose();
        }

        #endregion

        #region Helpers

        private void WriteInfo(FileTaskInfo info)
        {
            var builder = new StringBuilder(256);

            var tabs = new string(Tab, _offset.Count);

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

                builder.AppendFormat("{0}\"", new string('0', Size));

                builder.Append(" >");
            }
            else
                builder.Append(" />");

            _writer.WriteLine(builder.ToString());

            Processed(info);
        }

        private void UpdateInfo(FileTaskInfo info)
        {
            var currentOffset = _offset.Pop();

            _writer.WriteLine("{0}</dir>", new string(Tab, _offset.Count));
            _writer.Flush();

            var size = info.Attributes.FirstOrDefault(at => at.Name == "Size");
            if (size == null)
                return;

            var offset = currentOffset + Size - size.Value.Length;
            var currentPosition = _writer.BaseStream.Position;

            _file.Seek(offset, SeekOrigin.Begin);
            _writer = new StreamWriter(_file);

            _writer.Write(size.Value);
            _writer.Flush();

            _file.Seek(currentPosition, SeekOrigin.Begin);
            _writer = new StreamWriter(_file);
        }

        private void DeleteFile()
        {
            if (File.Exists(_filePath))
                File.Delete(_filePath);
        }

        #endregion
    }
}
