﻿using Microsoft.Win32;
using OCRPlayground.Core;
using OCRPlayground.Models;
using OCRPlayground.UI;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
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
            string methodname = CBMethod.SelectedItem?.ToString();
            if (methodname.IsNullOrEmpty() || Files.Count == 0)
            {
                MessageBox.Show("Please select a method and files to process");
                return;
            }

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
                methodInfo.Invoke(null, new object[] { item, progress });
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

            if (Files == null || Files.Count == 0)
            {
                MessageBox.Show("Please select files to process");
                return;
            }

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
                            int totalAmount = Files.Count;
                            int padValue = totalAmount.ToString().Length;
                            string progress = $"item: {(Files.IndexOf(file) + 1).ToString().PadLeft(padValue, '0')}/{Files.Count} progress: {(((double)Files.IndexOf(file) + 1) / ((double)Files.Count) * 100).ToString("F2")}%";
                            newItem.InputImagePath = file;
                            newItem.FillInputImage();
                            try
                            {

                                ImageProcessor.TryEachSetting(newItem, progress);
                            }
                            catch (Exception ex)
                            {
                                Trace.WriteLine(ex.Message);
                                throw;
                            }
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
                        int nullItemCount = ImageProcessor.MassResults.Count(x => x.Settings == setting && x.Type == type && x.ResultText.IsNullOrEmpty());
                        double nullItemRate = (double)nullItemCount / (double)ImageProcessor.MassResults.Count(x => x.Settings == setting && x.Type == type);
                        int numericCount = ImageProcessor.MassResults.Count(x => x.Settings == setting && x.Type == type && x.ResultText.IsNumeric());
                        double numericRate = (double)numericCount / (double)ImageProcessor.MassResults.Count(x => x.Settings == setting && x.Type == type);
                        AverageSettings.Add(new OCRAverageResults { Accuracy = average, MaxAccuracy = max, MinAccuracy = min, Settings = setting, Type = type, NullItemCount = nullItemCount, NullItemRate = nullItemRate, NumericCount = numericCount, NumericRate = numericRate });
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
            dt.Columns.Add("TextIsEmpty");

            foreach (var row in data)
            {
                dt.Rows.Add(new string[]
                {
                    row.Accuracy.ToString(),
                    row.Settings,
                    row.Type.ToString(),
                    row.ResultText.IsNullOrEmpty().ToString(),
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
            dt.Columns.Add("NullItemCount");
            dt.Columns.Add("NullItemRate");
            dt.Columns.Add("NumericCount");
            dt.Columns.Add("NumericRate");

            foreach (var row in data)
            {
                dt.Rows.Add(new string[]
                {
                    row.Accuracy.ToString(),
                    row.MinAccuracy.ToString(),
                    row.MaxAccuracy.ToString(),
                    row.Settings,
                    row.Type.ToString(),
                    row.NullItemCount.ToString(),
                    row.NullItemRate.ToString(),
                    row.NumericCount.ToString(),
                    row.NumericRate.ToString(),
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

        private void btnRandomPickFromFolder_Click(object sender, RoutedEventArgs e)
        {
            //Let user pick folder using default dialog
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
            //Initial Folder is Desktop
            folderDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            //Show Dialog
            folderDialog.ShowDialog();

            //Create a folder of similar name with postfix "_random" in the same directory
            var folderPath = folderDialog.SelectedPath;
            var folderName = System.IO.Path.GetFileName(folderPath);
            var newFolderPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(folderPath), folderName + "_random");
            System.IO.Directory.CreateDirectory(newFolderPath);

            //Get all files in the folder
            var files = System.IO.Directory.GetFiles(folderPath);

            //Copy 20% of the files to the new folder
            var random = new Random();
            var randomFiles = files.OrderBy(x => random.Next()).Take((int)(files.Count() * 0.2)).ToList();
            foreach (var file in randomFiles)
            {
                var newFilePath = System.IO.Path.Combine(newFolderPath, System.IO.Path.GetFileName(file));
                Trace.WriteLine($"Copying {file} to {newFilePath}");
                System.IO.File.Copy(file, newFilePath);
            }
        }
    }

    public class OCRAverageResults
    {
        public double Accuracy { get; set; }
        public double MinAccuracy { get; set; }
        public double MaxAccuracy { get; set; }
        public string Settings { get; set; }
        public string Type { get; set; }
        public int NullItemCount { get; set; }
        public double NullItemRate { get; set; }
        public int NumericCount { get; set; }
        public double NumericRate { get; set; }
    }
}
