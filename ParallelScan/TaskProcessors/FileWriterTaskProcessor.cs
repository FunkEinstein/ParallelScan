using ParallelScan.Info;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ParallelScan.TaskProcessors
{
    internal class FileWriterTaskProcessor : FileInfoTaskProcessor, IDisposable
    {
        public override event Action<TaskInfo> Processed = delegate { };

        private const char Tab = '\t';
        private const int DirectorySizeAttributeDimension = 19; // max number of digits in long

        private readonly Stack<long> _offset;

        private readonly string _filePath;
        private readonly FileStream _file;

        private StreamWriter _writer;

        public FileWriterTaskProcessor(string path)
        {
            _offset = new Stack<long>();
            _filePath = path;
            _file = new FileStream(_filePath, FileMode.Create, FileAccess.Write);
            _writer = new StreamWriter(_file);
        }

        #region Process

        protected override void ProcessInfo(TaskInfo info)
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

        #region Disposable

        public void Dispose()
        {
            if (IsSuccessfullyCompleted)
                _writer.Flush();

            _writer.Dispose();
            _file.Dispose();
        }

        #endregion

        #region Helpers

        private void WriteInfo(TaskInfo info)
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

                builder.AppendFormat("{0}\"", new string('0', DirectorySizeAttributeDimension));

                builder.Append(" >");
            }
            else
                builder.Append(" />");

            _writer.WriteLine(builder.ToString());

            Processed(info);
        }

        private void UpdateInfo(TaskInfo info)
        {
            var currentOffset = _offset.Pop();

            _writer.WriteLine("{0}</dir>", new string(Tab, _offset.Count));
            _writer.Flush();

            var size = info.Attributes.FirstOrDefault(at => at.Name == "Size");
            if (size.IsDefault())
                return;

            var offset = currentOffset + DirectorySizeAttributeDimension - size.Value.Length;
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
