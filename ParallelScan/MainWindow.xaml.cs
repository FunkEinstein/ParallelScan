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

namespace ParallelScan
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Stopwatch _watch;
        private readonly AutoResetEvent _event;

        private int _getCounter;
        private int _setCounter;
        private int _writeCounter;

        private bool _isGetComplete;
        private bool _isSetComplete;
        private bool _isWriteComplete;
        private bool _isMessageShown;

        private EventHandler _canceled = delegate { };


        public MainWindow()
        {
            InitializeComponent();

            _getCounter = 0;
            _setCounter = 0;
            _writeCounter = 0;

            _isGetComplete = false;
            _isSetComplete = false;
            _isWriteComplete = false;
            _isMessageShown = false;

            _event = new AutoResetEvent(true);
            _watch = new Stopwatch();
        }


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
                var treeWriter = new TreeWriterTaskProcessor(Dispatcher, dataProvider.Document, producer);
                var writer = new FileWriterTaskProcessor(savePath, producer);

                producer.Produced += OnGet;
                treeWriter.Processed += OnSet;
                writer.Processed += OnWrite;

                _canceled += producer.OnCanceled;

                producer.Failed += OnError;
                treeWriter.Failed += OnError;
                writer.Failed += OnError;

                producer.Completed += OnGetComplete;
                treeWriter.Completed += OnSetComplete;
                writer.Completed += OnWriteComplete;

                _watch.Start();
                producer.Start();

                StartMenuItem.IsEnabled = false;
                CancelMenuItem.IsEnabled = true;
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            _canceled(this, null);

            Dispatcher.Invoke(() =>
            {
                StartMenuItem.IsEnabled = true;
                CancelMenuItem.IsEnabled = false;
            });
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            _canceled(this, null);

            System.Windows.Application.Current.Shutdown();
        }

        private void OnWindowClose(object sender, EventArgs e)
        {
            _canceled(this, null);
        }


        private void OnGet(object sender, TaskInfo info)
        {
            if (info.TaskType == TaskType.Add)
                _getCounter++;

            Dispatcher.Invoke(() => GetCount.Text = _getCounter.ToString(CultureInfo.InvariantCulture));
        }

        private void OnSet(object sender, TaskInfo info)
        {
            _setCounter++;

            Dispatcher.Invoke(() => SetCount.Text = _setCounter.ToString(CultureInfo.InvariantCulture));
        }

        private void OnWrite(object sender, TaskInfo info)
        {
            _writeCounter++;

            Dispatcher.Invoke(() => WriteCount.Text = _writeCounter.ToString(CultureInfo.InvariantCulture));
        }

        private void OnError(object sender, Exception e)
        {
            _canceled(this, null);

            System.Windows.MessageBox.Show(e.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

            Dispatcher.Invoke(Clear);
        }


        private void OnGetComplete(object sender, EventArgs args)
        {
            _isGetComplete = true;

            _event.WaitOne();

            if (_isGetComplete && _isSetComplete && _isWriteComplete && !_isMessageShown)
                OnComplite();

            _event.Set();
        }

        private void OnSetComplete(object sender, EventArgs args)
        {
            _isSetComplete = true;

            _event.WaitOne();

            if (_isGetComplete && _isSetComplete && _isWriteComplete && !_isMessageShown)
                OnComplite();

            _event.Set();
        }

        private void OnWriteComplete(object sender, EventArgs args)
        {
            _isWriteComplete = true;

            _event.WaitOne();

            if (_isGetComplete && _isSetComplete && _isWriteComplete && !_isMessageShown)
                OnComplite();

            _event.Set();
        }

        private void OnComplite()
        {
            _isMessageShown = true;

            Dispatcher.Invoke(() =>
            {
                StartMenuItem.IsEnabled = true;
                CancelMenuItem.IsEnabled = false;
            });

            _watch.Stop();
            var time = Math.Round(_watch.Elapsed.TotalSeconds, 1);

            System.Windows.MessageBox.Show(
                string.Format("Сканирование окончено. Затраченно {0} сек.", time.ToString(CultureInfo.InvariantCulture)),
                "Информация",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }


        private void Clear()
        {
            StartMenuItem.IsEnabled = true;
            CancelMenuItem.IsEnabled = false;

            _getCounter = 0;
            GetCount.Text = _getCounter.ToString(CultureInfo.InvariantCulture);
            _setCounter = 0;
            SetCount.Text = _setCounter.ToString(CultureInfo.InvariantCulture);
            _writeCounter = 0;
            WriteCount.Text = _writeCounter.ToString(CultureInfo.InvariantCulture);

            _event.Set();

            _isMessageShown = false;
            _watch.Reset();
        }
    }
}
