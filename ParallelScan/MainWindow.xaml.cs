using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using ParallelScan.TaskProducers;
using ParallelScan.TaskProcessors;
using System.Diagnostics;
using ParallelScan.Info;
using ParallelScan.TaskCoordinator;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ParallelScan
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Stopwatch _watch;
        private readonly AutoResetEvent _event;

        private int _fileCounter;
        private int _setCounter;
        private int _writeCounter;

        private bool _isGetComplete;
        private bool _isSetComplete;
        private bool _isWriteComplete;
        private bool _isMessageShown;

        private TaskCoordinator<FileTaskInfo> _coordinator;

        public MainWindow()
        {
            InitializeComponent();

            _fileCounter = 0;
            _setCounter = 0;
            _writeCounter = 0;

            _isGetComplete = false;
            _isSetComplete = false;
            _isWriteComplete = false;
            _isMessageShown = false;

            _event = new AutoResetEvent(true);
            _watch = new Stopwatch();
        }

        #region GUI handlers

        private void OnStartClick(object sender, RoutedEventArgs e)
        {
            Clear();

            var folderToScanDialog = new FolderBrowserDialog();
            var fileToSave = new SaveFileDialog();

            fileToSave.CheckPathExists = true;
            fileToSave.AddExtension = true;
            fileToSave.DefaultExt = "xml";
            fileToSave.Filter = @"XML files (*.xml)|*.xml";
            fileToSave.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (folderToScanDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK &&
                fileToSave.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var savePath = fileToSave.FileName;
                var slectedPath = folderToScanDialog.SelectedPath;
                var info = new DirectoryInfo(slectedPath);

                var dataProvider = FindResource("xmlDataProvider") as XmlDataProvider;

                if (dataProvider == null)
                    throw new ResourceReferenceKeyNotFoundException();

                var document = new XmlDocument();
                var node = document.CreateElement("dir");
                var attr = document.CreateAttribute("Name");
                attr.InnerText = info.Name;
                node.Attributes.Append(attr);

                document.AppendChild(node);

                dataProvider.Document = document;

                var producer = new FileInfoTaskProducer(slectedPath);
                var treeWriter = new TreeWriterInfoTaskProcessor(Dispatcher, dataProvider.Document);
                var fileWriter = new FileWriterTaskProcessor(savePath);

                _coordinator = new TaskCoordinator<FileTaskInfo>(producer, treeWriter, fileWriter);

                // producer.Produced += OnGet;
                // treeWriter.Processed += OnSet;
                // fileWriter.Processed += OnWrite;

                _coordinator.Failed += OnError;
                _coordinator.Completed += OnComplited;
                _coordinator.Produced += OnFileProcessed;

                _watch.Start();
                _coordinator.Start();

                StartMenuItem.IsEnabled = false;
                CancelMenuItem.IsEnabled = true;
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Cancel();

            Dispatcher.Invoke(() =>
            {
                StartMenuItem.IsEnabled = true;
                CancelMenuItem.IsEnabled = false;
            });
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            Cancel();

            Application.Current.Shutdown();
        }

        private void OnWindowClose(object sender, EventArgs e)
        {
            Cancel();
        }

        #endregion

        #region Event handlers

        private void OnFileProcessed(FileTaskInfo info)
        {
            if (info.TaskType == TaskType.Add)
                _fileCounter++;

            Dispatcher.Invoke(() => GetCount.Text = _fileCounter.ToString(CultureInfo.InvariantCulture));
        }

        // private void OnSet(object sender, FileTaskInfo info)
        // {
        //     _setCounter++;
        //
        //     Dispatcher.Invoke(() => SetCount.Text = _setCounter.ToString(CultureInfo.InvariantCulture));
        // }
        //
        // private void OnWrite(object sender, FileTaskInfo info)
        // {
        //     _writeCounter++;
        //
        //     Dispatcher.Invoke(() => WriteCount.Text = _writeCounter.ToString(CultureInfo.InvariantCulture));
        // }

        private void OnError(Exception ex)
        {
            Cancel();

            MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

            Dispatcher.Invoke(Clear);
        }

        private void OnComplited()
        {
            _isMessageShown = true;

            Dispatcher.Invoke(() =>
            {
                StartMenuItem.IsEnabled = true;
                CancelMenuItem.IsEnabled = false;
            });

            _watch.Stop();
            var time = Math.Round(_watch.Elapsed.TotalSeconds, 1);

            MessageBox.Show(
                string.Format("Сканирование окончено. Затраченно {0} сек.", time.ToString(CultureInfo.InvariantCulture)),
                "Информация",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        #endregion

        #region Helpers

        private void Cancel()
        {
            if (_coordinator == null)
                return;

            _coordinator.Cancel();
            _coordinator = null;
        }

        private void Clear()
        {
            StartMenuItem.IsEnabled = true;
            CancelMenuItem.IsEnabled = false;

            _fileCounter = 0;
            GetCount.Text = _fileCounter.ToString(CultureInfo.InvariantCulture);
            _setCounter = 0;
            SetCount.Text = _setCounter.ToString(CultureInfo.InvariantCulture);
            _writeCounter = 0;
            WriteCount.Text = _writeCounter.ToString(CultureInfo.InvariantCulture);

            _event.Set();

            _isMessageShown = false;
            _watch.Reset();
        }

        #endregion
    }
}
