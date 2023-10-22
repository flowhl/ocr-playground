using OCRPlayground.Core;
using OCRPlayground.Models;
using OCRPlayground.UI;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            foreach(var item in Items)
            {
                methodInfo.Invoke(null, new object[] { item });
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

            ResultText.Content = $"Processed: {Items.Count} Images \nAverage Accuracy: {Math.Round(Items.Average(x => x.Accuracy),2)}\nMax Accuracy: {Math.Round(Items.Max(x => x.Accuracy),2)} \nMin Accuracy: {Math.Round(Items.Min(x => x.Accuracy),2)}";
        }
    }
}
