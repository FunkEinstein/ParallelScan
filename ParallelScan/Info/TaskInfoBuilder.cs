using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

namespace ParallelScan.Info
{
    class TaskInfoBuilder
    {
        private readonly WindowsPrincipal _currentUser;

        public TaskInfoBuilder()
        {
            var identity = WindowsIdentity.GetCurrent();
            _currentUser = new WindowsPrincipal(identity);

        }

        public TaskInfo Build(FileInfo fileInfo)
        {
            var attributes = GetFilesAttributes(fileInfo).ToList();

            return new TaskInfo
            {
                Attributes = attributes,
                Name = fileInfo.Name,
                IsDirectory = false,
                TaskType = TaskType.Add
            };
        }

        public TaskInfo Build(DirectoryInfo directoryInfo)
        {
            var attributes = GetDirectoryAttributes(directoryInfo).ToList();

            return new TaskInfo
            {
                Attributes = attributes,
                Name = directoryInfo.Name,
                IsDirectory = true,
                TaskType = TaskType.Add
            };
        }

        public TaskInfo Build(DirectoryInfo directoryInfo, long size)
        {
            return new TaskInfo
            {
                Attributes = new List<InfoAttribute> { new InfoAttribute("Size", size.ToString(CultureInfo.InvariantCulture)) },
                Name = directoryInfo.Name,
                IsDirectory = true,
                TaskType = TaskType.Update
            };
        }
        
        #region Helpers

        private IEnumerable<InfoAttribute> GetDirectoryAttributes(DirectoryInfo dir)
        {
            var attributes = new List<InfoAttribute>();

            attributes.Add(new InfoAttribute("CreationTime", dir.CreationTime.ToString(CultureInfo.InvariantCulture)));
            attributes.Add(new InfoAttribute("LastAccessTime", dir.LastAccessTime.ToString(CultureInfo.InvariantCulture)));
            attributes.Add(new InfoAttribute("LastWriteTime", dir.LastWriteTime.ToString(CultureInfo.InvariantCulture)));
            attributes.Add(new InfoAttribute("Attributes", dir.Attributes.ToString()));

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
                var ownerNt = ownerIdentity.Translate(typeof(NTAccount));
                attributes.Add(new InfoAttribute("Owner", ownerNt.Value));
            }
            catch (IdentityNotMappedException)
            {
                attributes.Add(new InfoAttribute("Owner", ownerIdentity.Value));
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

            attributes.Add(new InfoAttribute("UserRights", rights));

            return attributes;
        }

        private IEnumerable<InfoAttribute> GetFilesAttributes(FileInfo file)
        {
            var attributes = new List<InfoAttribute>();

            attributes.Add(new InfoAttribute("CreationTime", file.CreationTime.ToString(CultureInfo.InvariantCulture)));
            attributes.Add(new InfoAttribute("LastAccessTime", file.LastAccessTime.ToString(CultureInfo.InvariantCulture)));
            attributes.Add(new InfoAttribute("LastWriteTime", file.LastWriteTime.ToString(CultureInfo.InvariantCulture)));
            attributes.Add(new InfoAttribute("Attributes", file.Attributes.ToString()));

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

            var ownerSi = security.GetOwner(typeof(SecurityIdentifier));
            try
            {
                var ownerNt = ownerSi.Translate(typeof(NTAccount));
                attributes.Add(new InfoAttribute("Owner", ownerNt.Value));
            }
            catch (IdentityNotMappedException)
            {
                attributes.Add(new InfoAttribute("Owner", ownerSi.Value));
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

            attributes.Add(new InfoAttribute("UserRights", rights));
            attributes.Add(new InfoAttribute("Size", file.Length.ToString(CultureInfo.InvariantCulture)));

            return attributes;
        }

        #endregion
    }
}
