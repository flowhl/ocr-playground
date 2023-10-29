using OCRPlayground.Core;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCRPlayground.Models
{
    public class OCRItem : IDisposable
    {
        public List<Mat> ResultImages { get; set; } = new List<Mat>();
        public double Accuracy { get; set; }
        public Mat InputImage { get; set; }
        public string InputImagePath { get; set; }
        public string Text { get; set; }
        public OCRItem() { }

        public void FillInputImage()
        {
            if (InputImagePath.IsNullOrEmpty()) throw new ArgumentException("InputImage");
            if(!File.Exists(InputImagePath)) throw new FileNotFoundException(InputImagePath);
            InputImage = new Mat(InputImagePath);
            Trace.WriteLine($"Loaded {InputImagePath}");
        }

        public void Dispose()
        {
            ResultImages.Clear();
            ResultImages = null;
            Accuracy = 0;
            InputImage = null;
            InputImagePath = null;
            Text = null;
            GC.Collect();
        }

        public List<Tuple<double, string>> MassAccuracy { get; set; } = new List<Tuple<double, string>>();
    }
}
