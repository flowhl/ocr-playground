using OCRPlayground.Models;
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

namespace OCRPlayground.UI
{
    /// <summary>
    /// Interaction logic for ResultControl.xaml
    /// </summary>
    public partial class ResultControl : UserControl
    {
        public ResultControl()
        {
            InitializeComponent();
        }
        public void SetData(OCRItem item)
        {
            if (item == null) return;
            Accuracy.Content = "Accuracy: " + Math.Round(item.Accuracy, 2) + " " + "Text: " + item.Text;
            if (item.ResultImages == null) return;
            foreach (var res in item.ResultImages)
            {
                var img = new Image();
                img.Source = res.ToBitmapSource();
                img.Height = 100;
                img.Margin = new Thickness(5);
                SPImages.Children.Add(img);
            }

        }
    }
}
