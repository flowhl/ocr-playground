using Microsoft.Win32;
using OCRPlayground.Core;
using OCRPlayground.Models;
using OCRPlayground.UI;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static OpenCvSharp.Stitcher;

namespace OCRPlayground
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public List<OCRItem> Items { get; set; } = new List<OCRItem>();
        public List<string> Files { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            FillDropDown();
        }

        public void FillDropDown()
        {
            CBMethod.Items.Clear();
            var type = typeof(ImageProcessor);
            var methods = type.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).Where(x => x.GetParameters().Any(x => x.ParameterType.Name == "OCRItem"));
            var names = methods.Select(x => x.Name).ToList();

            foreach (var name in names)
            {
                CBMethod.Items.Add(name);
            }
        }

        private void btnSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog object
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            // Configure dialog settings
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            openFileDialog.Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif";
            openFileDialog.Multiselect = true;

            // Show dialog and get result
            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                // Store selected file paths into your Files list
                Files = openFileDialog.FileNames.ToList();
            }
        }

        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            Run();
        }

        public void Run()
        {
            //get processing method
            string methodname = CBMethod.SelectedItem.ToString();
            if (methodname.IsNullOrEmpty()) return;
            var methodInfo = typeof(ImageProcessor).GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).Where(x => x.GetParameters().Any(x => x.ParameterType.Name == "OCRItem") && x.Name == methodname).FirstOrDefault();

            //create items
            Items.Clear();
            foreach (var file in Files)
            {
                var newItem = new OCRItem();
                newItem.InputImagePath = file;
                newItem.FillInputImage();
                Items.Add(newItem);
            }

            //process each item
            foreach (var item in Items)
            {
                string progress = $"item: {Items.IndexOf(item) + 1}/{Items.Count}";
                methodInfo.Invoke(null, new object[] { item, progress});
            }

            //display 
            SPInput.Children.Clear();
            SPOutput.Children.Clear();
            foreach (var item in Items)
            {
                //input
                var img = new Image();
                img.Source = item.InputImage.ToBitmapSource();
                img.Height = 100;
                img.Margin = new Thickness(5);
                SPInput.Children.Add(img);


                //output
                if (item.ResultImages == null || item.ResultImages.Count() == 0) continue;
                var res = new ResultControl();
                res.SetData(item);
                SPOutput.Children.Add(res);
            }

            ResultText.Content = $"Processed: {Items.Count} Images \nAverage Accuracy: {Math.Round(Items.Average(x => x.Accuracy), 2)}\nMax Accuracy: {Math.Round(Items.Max(x => x.Accuracy), 2)} \nMin Accuracy: {Math.Round(Items.Min(x => x.Accuracy), 2)}";
        }

        public async void RunMassTest()
        {
            DateTime Start = DateTime.Now;

            List<Task> taskList = new List<Task>();
            SemaphoreSlim semaphore = new SemaphoreSlim(12);

            foreach (var file in Files)
            {
                await semaphore.WaitAsync();

                var task = Task.Run(() =>
                {
                    try
                    {
                        using (var newItem = new OCRItem())
                        {
                            string progress = $"item: {Files.IndexOf(file) + 1}/{Files.Count}";
                            newItem.InputImagePath = file;
                            newItem.FillInputImage();
                            ImageProcessor.TryEachSetting(newItem, progress);
                        }
                        GC.Collect();
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                taskList.Add(task);
            }

            Task.WaitAll(taskList.ToArray());
            

            List<OCRAverageResults> AverageSettings = new List<OCRAverageResults>();
            lock (ImageProcessor.MassResults)
            {

                var AllSettings = ImageProcessor.MassResults.Select(x => x.Settings).Distinct().ToList();
                var AllTypes = ImageProcessor.MassResults.Select(x => x.Type).Distinct().ToList();
                foreach (var type in AllTypes)
                {
                    foreach (var setting in AllSettings)
                    {
                        double average = ImageProcessor.MassResults.Where(x => x.Settings == setting && x.Type == type).Average(x => x.Accuracy);
                        double max = ImageProcessor.MassResults.Where(x => x.Settings == setting && x.Type == type).Max(x => x.Accuracy);
                        double min = ImageProcessor.MassResults.Where(x => x.Settings == setting && x.Type == type).Min(x => x.Accuracy);
                        AverageSettings.Add(new OCRAverageResults { Accuracy = average, MaxAccuracy = max, MinAccuracy = min, Settings = setting, Type = type });
                    }
                }

                DateTime End = DateTime.Now;

                var duration = End - Start;

                AverageSettings = AverageSettings.OrderByDescending(x => x.Accuracy).ToList();

                ExportRawCSV(ImageProcessor.MassResults);
                ExportGroupedCSV(AverageSettings);

            }
        }

        public void ExportRawCSV(List<OCRResultData> data)
        {
            var dt = new DataTable();
            dt.Columns.Add("Accuracy");
            dt.Columns.Add("ScaleUp;EnsureBlackText;ApplyNonLocalMeansDenoisingSettings;ApplyBilateralFilterSettings;ApplyMedianFilterSettings;ApplyGaussianBlurSettings;Threshold");
            dt.Columns.Add("Type");

            foreach (var row in data)
            {
                dt.Rows.Add(new string[]
                {
                    row.Accuracy.ToString(),
                    row.Settings,
                    row.Type.ToString(),
                });
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "CSV files (*.csv)|*.csv";
            saveFileDialog.InitialDirectory = "D:\\Github_repos\\xstrat\\OCRPlayground\\results";
            saveFileDialog.FileName = "raw_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";

            var res = saveFileDialog.ShowDialog();

            if (res ?? false)
            {
                CSVHelper.SaveDataTableToCSV(dt, saveFileDialog.FileName);
            }
        }



        public void ExportGroupedCSV(List<OCRAverageResults> data)
        {
            var dt = new DataTable();
            dt.Columns.Add("Accuracy");
            dt.Columns.Add("MinAccuracy");
            dt.Columns.Add("MaxAccuracy");
            dt.Columns.Add("ScaleUp;EnsureBlackText;ApplyNonLocalMeansDenoisingSettings;ApplyBilateralFilterSettings;ApplyMedianFilterSettings;ApplyGaussianBlurSettings;Threshold");
            dt.Columns.Add("Type");

            foreach (var row in data)
            {
                dt.Rows.Add(new string[]
                {
                    row.Accuracy.ToString(),
                    row.MinAccuracy.ToString(),
                    row.MaxAccuracy.ToString(),
                    row.Settings,
                    row.Type.ToString(),
                });
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "CSV files (*.csv)|*.csv";
            saveFileDialog.InitialDirectory = "D:\\Github_repos\\xstrat\\OCRPlayground\\results";
            saveFileDialog.FileName = "grouped_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";

            var res = saveFileDialog.ShowDialog();

            if (res ?? false)
            {
                CSVHelper.SaveDataTableToCSV(dt, saveFileDialog.FileName);
            }
        }

        private void btnMassTest_Click(object sender, RoutedEventArgs e)
        {
            RunMassTest();
        }
    }

    public class OCRAverageResults
    {
        public double Accuracy { get; set; }
        public double MinAccuracy { get; set; }
        public double MaxAccuracy { get; set; }
        public string Settings { get; set; }
        public string Type { get; set; }
    }
}
