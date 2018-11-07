using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using System.Diagnostics;
using ParallelScan.TaskCoordinator;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ParallelScan
{
    public partial class MainWindow : Window
    {
        private readonly Stopwatch _watch;

        private int _scannedItemCount;
        private int _wroteToFileCount;
        private int _wroteToTreeCount;

        private ScanCoordinator _coordinator;

        public MainWindow()
        {
            InitializeComponent();

            _scannedItemCount = 0;
            _wroteToTreeCount = 0;
            _wroteToFileCount = 0;

            _watch = new Stopwatch();
        }

        #region GUI handlers

        private void OnStartClick(object sender, RoutedEventArgs e)
        {
            Clear();

            OpenFolderSelectionDialog();
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

        private void OnItemScanned()
        {
            _scannedItemCount++;
            Dispatcher.Invoke(() => GetCount.Text = _scannedItemCount.ToString(CultureInfo.InvariantCulture));
        }

        private void OnItemWroteToFile()
        {
            _wroteToFileCount++;
            Dispatcher.Invoke(() => WriteCount.Text = _wroteToFileCount.ToString(CultureInfo.InvariantCulture));
        }

        private void OnItemWroteToTree()
        {
            _wroteToTreeCount++;
            Dispatcher.Invoke(() => SetCount.Text = _wroteToTreeCount.ToString(CultureInfo.InvariantCulture));
        }

        private void OnError(Exception ex)
        {
            Cancel();

            MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

            Dispatcher.Invoke(Clear);
        }

        private void OnComplited()
        {
            Dispatcher.Invoke(() =>
            {
                StartMenuItem.IsEnabled = true;
                CancelMenuItem.IsEnabled = false;
            });

            _watch.Stop();
            var time = Math.Round(_watch.Elapsed.TotalSeconds, 1);

            _coordinator = null;

            MessageBox.Show(
                string.Format("Сканирование окончено. Затраченно {0} сек.", time.ToString(CultureInfo.InvariantCulture)),
                "Информация",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        #endregion

        #region Helpers

        private void OpenFolderSelectionDialog()
        {
            var folderToScanDialog = new FolderBrowserDialog();
            var fileToSave = new SaveFileDialog();

            fileToSave.CheckPathExists = true;
            fileToSave.AddExtension = true;
            fileToSave.DefaultExt = "xml";
            fileToSave.Filter = @"XML files (*.xml)|*.xml";
            fileToSave.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (folderToScanDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK &&
                fileToSave.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                Start(fileToSave.FileName, folderToScanDialog.SelectedPath);
        }

        private void Start(string saveResultPath, string selectedDirectoryPath)
        {
            var info = new DirectoryInfo(selectedDirectoryPath);

            var dataProvider = ConfigureXmlDataProvider(info.Name);
            var coordinator = ConfigureScan(selectedDirectoryPath, saveResultPath, dataProvider);

            _watch.Start();
            coordinator.Start();

            StartMenuItem.IsEnabled = false;
            CancelMenuItem.IsEnabled = true;
        }

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

            _scannedItemCount = 0;
            GetCount.Text = _scannedItemCount.ToString(CultureInfo.InvariantCulture);
            _wroteToTreeCount = 0;
            SetCount.Text = _wroteToTreeCount.ToString(CultureInfo.InvariantCulture);
            _wroteToFileCount = 0;
            WriteCount.Text = _wroteToFileCount.ToString(CultureInfo.InvariantCulture);

            _watch.Reset();
        }

        private XmlDataProvider ConfigureXmlDataProvider(string directoryName)
        {
            var dataProvider = FindResource("xmlDataProvider") as XmlDataProvider;
            if (dataProvider == null)
                throw new ResourceReferenceKeyNotFoundException();

            var document = new XmlDocument();
            var node = document.CreateElement("dir");
            var attr = document.CreateAttribute("Name");
            attr.InnerText = directoryName;
            node.Attributes.Append(attr);

            document.AppendChild(node);

            dataProvider.Document = document;

            return dataProvider;
        }

        private ScanCoordinator ConfigureScan(string selectedDirectoryPath, string saveResultPath, XmlDataProvider dataProvider)
        {
            _coordinator = new ScanCoordinator(selectedDirectoryPath, saveResultPath, dataProvider.Document, Dispatcher);

            _coordinator.Failed += OnError;
            _coordinator.Completed += OnComplited;

            _coordinator.ItemScanned += OnItemScanned;
            _coordinator.ItemWroteToFile += OnItemWroteToFile;
            _coordinator.ItemWroteToTree += OnItemWroteToTree;

            return _coordinator;
        }

        #endregion
    }
}
