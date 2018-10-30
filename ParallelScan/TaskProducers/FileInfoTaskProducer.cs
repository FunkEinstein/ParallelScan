using System.Globalization;
using System.Text.RegularExpressions;
using ParallelScan.Info;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using My = ParallelScan.Info;

namespace ParallelScan.TaskProducers
{
    class FileInfoTaskProducer : ITaskProducer<FileTaskInfo>
    {
        private Task _task;
        private readonly CancellationTokenSource _cts;

        private readonly WindowsPrincipal _currentUser;

        private readonly string _baseDirectoryPath;

        public event Action<FileTaskInfo> Produced = delegate { };
        public event Action<Exception> Failed = delegate { };
        public event Action Completed = delegate { };

        public FileInfoTaskProducer(string baseDirectoryPath)
        {
            var identity = WindowsIdentity.GetCurrent();
            _currentUser = new WindowsPrincipal(identity);

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

            Produced(GetDirectoriesInfo(info));

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
            catch (UnauthorizedAccessException)
            {
                var taskInfo = new FileTaskInfo
                {
                    Attributes = new List<My.Attribute>(),
                    IsDirectory = true,
                    Name = info.Name,
                    TaskType = TaskType.Update
                };

                Produced(taskInfo);

                return size;
            }

            for (int i = 0; i < dirs.Count(); i++)
            {
                Produced(GetDirectoriesInfo(dirs[i]));

                size += GetStructure(dirs[i]);
            }

            var files = info.GetFiles();
            for (int i = 0; i < files.Count(); i++)
            {
                Produced(GetFileInfo(files[i]));

                size += files[i].Length;
            }

            var updateSizeInfo = new FileTaskInfo
            {
                Attributes = new List<My.Attribute> { new My.Attribute("Size", size.ToString(CultureInfo.InvariantCulture)) },
                Name = info.Name,
                IsDirectory = true,
                TaskType = TaskType.Update
            };
            Produced(updateSizeInfo);

            return size;
        }

        private FileTaskInfo GetDirectoriesInfo(DirectoryInfo dir)
        {
            var attributes = GetDirectoryAttributes(dir).ToList();

            return new FileTaskInfo
            {
                Attributes = attributes,
                Name = dir.Name,
                IsDirectory = true,
                TaskType = TaskType.Add
            };
        }

        private FileTaskInfo GetFileInfo(FileInfo file)
        {
            var attributes = GetFilesAttributes(file).ToList();

            return new FileTaskInfo
            {
                Attributes = attributes,
                Name = file.Name,
                IsDirectory = false,
                TaskType = TaskType.Add
            };
        }

        private IEnumerable<My.Attribute> GetDirectoryAttributes(DirectoryInfo dir)
        {
            var attributes = new List<My.Attribute>();

            attributes.Add(new My.Attribute("CreationTime", dir.CreationTime.ToString(CultureInfo.InvariantCulture)));
            attributes.Add(new My.Attribute("LastAccessTime", dir.LastAccessTime.ToString(CultureInfo.InvariantCulture)));
            attributes.Add(new My.Attribute("LastWriteTime", dir.LastWriteTime.ToString(CultureInfo.InvariantCulture)));
            attributes.Add(new My.Attribute("Attributes", dir.Attributes.ToString()));

            DirectorySecurity security;
            try
            {
                security = dir.GetAccessControl();
            }
            catch (UnauthorizedAccessException)
            {
                return attributes;
            }
            catch (InvalidOperationException)
            {
                return attributes;
            }

            var ownerIdentity = security.GetOwner(typeof(SecurityIdentifier));
            try
            {
                var ownerNT = ownerIdentity.Translate(typeof(NTAccount));
                attributes.Add(new My.Attribute("Owner", ownerNT.Value));
            }
            catch (IdentityNotMappedException)
            {
                attributes.Add(new My.Attribute("Owner", ownerIdentity.Value));
            }

            var rules = security.GetAccessRules(true, true, typeof(NTAccount));

            var allow = new HashSet<FileSystemRights>();
            var deny = new HashSet<FileSystemRights>();

            foreach (FileSystemAccessRule rule in rules)
            {
                if (_currentUser.IsInRole(rule.IdentityReference.Value))
                {
                    var regex = new Regex(@"-?\d+");
                    var match = regex.Match(rule.FileSystemRights.ToString());
                    if (match.Success)
                        continue;

                    switch (rule.AccessControlType)
                    {
                        case AccessControlType.Allow:
                            allow.Add(rule.FileSystemRights);
                            break;

                        case AccessControlType.Deny:
                            deny.Add(rule.FileSystemRights);
                            break;
                    }
                }
            }

            var builder = new StringBuilder();

            builder.Append("Allow:");
            foreach (var item in allow)
            {
                builder.Append(" ");
                builder.Append(item.ToString());
                builder.Append(",");
            }
            if (allow.Count != 0)
                builder.Replace(',', ';', builder.Length - 1, 1);

            builder.Append(" Deny:");
            foreach (var item in deny)
            {
                builder.Append(" ");
                builder.Append(item.ToString());
                builder.Append(",");
            }
            if (deny.Count != 0)
                builder.Replace(',', ';', builder.Length - 1, 1);

            var rights = builder.ToString();

            attributes.Add(new My.Attribute("UserRights", rights));

            return attributes;
        }

        private IEnumerable<My.Attribute> GetFilesAttributes(FileInfo file)
        {
            var attributes = new List<My.Attribute>();

            attributes.Add(new My.Attribute("CreationTime", file.CreationTime.ToString(CultureInfo.InvariantCulture)));
            attributes.Add(new My.Attribute("LastAccessTime", file.LastAccessTime.ToString(CultureInfo.InvariantCulture)));
            attributes.Add(new My.Attribute("LastWriteTime", file.LastWriteTime.ToString(CultureInfo.InvariantCulture)));
            attributes.Add(new My.Attribute("Attributes", file.Attributes.ToString()));

            FileSecurity security;
            try
            {
                security = file.GetAccessControl();
            }
            catch (UnauthorizedAccessException)
            {
                return attributes;
            }
            catch (InvalidOperationException)
            {
                return attributes;
            }

            var ownerSI = security.GetOwner(typeof(SecurityIdentifier));
            try
            {
                var ownerNT = ownerSI.Translate(typeof(NTAccount));
                attributes.Add(new My.Attribute("Owner", ownerNT.Value));
            }
            catch (IdentityNotMappedException)
            {
                attributes.Add(new My.Attribute("Owner", ownerSI.Value));
            }

            var rules = security.GetAccessRules(true, true, typeof(NTAccount));

            var allow = new HashSet<FileSystemRights>();
            var deny = new HashSet<FileSystemRights>();

            foreach (FileSystemAccessRule rule in rules)
            {
                if (_currentUser.IsInRole(rule.IdentityReference.Value))
                    switch (rule.AccessControlType)
                    {
                        case AccessControlType.Allow:
                            allow.Add(rule.FileSystemRights);
                            break;

                        case AccessControlType.Deny:
                            deny.Add(rule.FileSystemRights);
                            break;
                    }
            }

            var builder = new StringBuilder();

            builder.Append("Allow:");
            foreach (var item in allow)
            {
                builder.Append(" ");
                builder.Append(item.ToString());
                builder.Append(",");
            }
            if (allow.Count != 0)
                builder.Remove(builder.Length - 1, 1);

            builder.Append(" Deny:");
            foreach (var item in deny)
            {
                builder.Append(" ");
                builder.Append(item.ToString());
                builder.Append(",");
            }
            if (deny.Count != 0)
                builder.Remove(builder.Length - 1, 1);

            var rights = builder.ToString();

            attributes.Add(new My.Attribute("UserRights", rights));
            attributes.Add(new My.Attribute("Size", file.Length.ToString(CultureInfo.InvariantCulture)));

            return attributes;
        }

        #endregion
    }
}
