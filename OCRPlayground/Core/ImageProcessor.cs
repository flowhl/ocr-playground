using OCRPlayground.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCRPlayground.Core
{
    public static class ImageProcessor
    {
        public static void NoPreprocessing(OCRItem item)
        {
            item.ResultImages = new List<OpenCvSharp.Mat>();
            item.ResultImages.Add(item.InputImage);
            OCRHelper.ProcessWithTesseract(item);
        }

        public static void Otsu(OCRItem item)
        {
            item.ResultImages = new List<OpenCvSharp.Mat>();
            item.ResultImages.Add(item.InputImage);
            item.ResultImages.Add(OCRHelper.ApplyOtsu(item.InputImage));
            OCRHelper.ProcessWithTesseract(item);
        }
    }
}
