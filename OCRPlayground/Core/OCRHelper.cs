using OCRPlayground.Models;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;

namespace OCRPlayground.Core
{
    public static class OCRHelper
    {
        public static void ProcessWithTesseract(OCRItem item)
        {
            if (item.InputImage.Empty()) return;

            using (var engine = new TesseractEngine(@"D:\Github_repos\xstrat\Enterprise\xstrat-client\xstrat\External\tessdata", "eng", EngineMode.Default))
            {
                // Limit the characters Tesseract uses for OCR
                //engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.-_,;: ");

                // Run OCR on the image and retrieve the recognized text
                using (var page = engine.Process(OCRHelper.GetPixFromMat(item.ResultImages.Last()), PageSegMode.SingleLine))
                {
                    var text = page.GetText().Trim();

                    item.Accuracy = page.GetMeanConfidence();

                    Trace.WriteLine("Extracted text: " + text);
                    item.Text = text;
                }
            }
        }
        public static Pix GetPixFromMat(Mat image)
        {
            if (image.Empty()) return null;
            byte[] bytes = image.ToBytes();
            return Pix.LoadFromMemory(bytes);
        }
        public static Mat ApplyOtsu(Mat src)
        {
            // Convert the color image to grayscale
            Mat srcGray = new Mat();
            Cv2.CvtColor(src, srcGray, ColorConversionCodes.BGR2GRAY);

            // Create an output Mat object to store the thresholded image
            Mat dst = new Mat();

            // Apply Otsu's thresholding
            Cv2.Threshold(srcGray, dst, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

            return dst;
        }
    }
}
