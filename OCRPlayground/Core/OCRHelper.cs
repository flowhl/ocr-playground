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
        public static void ProcessWithTesseract(OCRItem item, bool useMassAccuracy = false, string settingsstring = "")
        {
            if (item.InputImage.Empty()) return;

            using (var engine = new TesseractEngine(@"D:\Github_repos\xstrat\Enterprise\xstrat-client\xstrat\External\tessdata", "eng", EngineMode.Default))
            {
                // Limit the characters Tesseract uses for OCR
                engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.-_,;: ");

                // Run OCR on the image and retrieve the recognized text
                using (var page = engine.Process(OCRHelper.GetPixFromMat(item.ResultImages.Last()), PageSegMode.SingleLine))
                {
                    var text = page.GetText().Trim();

                    if (!useMassAccuracy)
                        item.Accuracy = page.GetMeanConfidence();
                    else
                    {
                        string type = "";
                        if (item.InputImagePath.Contains("$name"))
                        {
                            type = "name";
                        }
                        else if (item.InputImagePath.Contains("$score"))
                        {
                            type = "score";
                        }
                        else if (item.InputImagePath.Contains("$teamname"))
                        {
                            type = "teamname";
                        }
                        else if (item.InputImagePath.Contains("time"))
                        {
                            type = "time";
                        }

                        if (text.Length > 4 || type == "time" || type == "score")
                        {
                            lock (ImageProcessor.MassResults)
                            {
                                ImageProcessor.MassResults.Add(new OCRResultData { Accuracy = page.GetMeanConfidence(), Settings = settingsstring, Type = type });
                            }
                            item.MassAccuracy.Add(new Tuple<double, string>(page.GetMeanConfidence(), settingsstring));
                        }
                    }

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

        public static Mat ApplyThreshold(Mat src, ThresholdTypes types = ThresholdTypes.Binary | ThresholdTypes.Otsu)
        {
            // Create an output Mat object to store the thresholded image
            Mat dst = new Mat();

            // Apply Otsu's thresholding
            Cv2.Threshold(src, dst, 0, 255, types);

            return dst;
        }

        public static Mat ApplyGaussianBlur(Mat src, double size = 5, double sigma = 1.5)
        {
            // Create an output Mat object to store the denoised image
            Mat dst = new Mat();

            // Apply Gaussian blur to remove noise
            Cv2.GaussianBlur(src, dst, new Size(size, size), sigma, sigma);

            return dst;
        }

        public static Mat ConvertToGray(Mat src)
        {
            // Create an output Mat object to store the grayscale image
            Mat dst = new Mat();

            // Convert the color image to grayscale
            Cv2.CvtColor(src, dst, ColorConversionCodes.BGR2GRAY);

            return dst;
        }

        //denoising
        public static Mat ApplyMedianFilter(Mat src, int distance = 5)
        {
            Mat dst = new Mat();
            Cv2.MedianBlur(src, dst, distance);
            return dst;
        }

        //denoising
        public static Mat ApplyBilateralFilter(Mat src, int d = 9, double sigma = 75)
        {
            Mat dst = new Mat();
            Cv2.BilateralFilter(src, dst, d, sigma, sigma);
            return dst;
        }

        //denoising
        public static Mat ApplyNonLocalMeansDenoising(Mat src, int h = 30, int templatewindowSize = 7, int searchWindowSize = 21)
        {
            Mat dst = new Mat();
            Cv2.FastNlMeansDenoisingColored(src, dst, h, h, templatewindowSize, searchWindowSize);
            return dst;
        }

        public static Mat EnsureBlackTextOnWhite(Mat src)
        {
            // Convert the image to grayscale if it's not already
            Mat gray = new Mat();
            if (src.Channels() == 3)
            {
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            }
            else
            {
                gray = src.Clone();
            }

            // Count the number of white pixels
            int whitePixels = Cv2.CountNonZero(gray);

            // Get the total number of pixels
            int totalPixels = gray.Rows * gray.Cols;

            // Create an output Mat object to store the final image
            Mat dst = new Mat();

            // If the number of white pixels is less than half of total pixels,
            // it's likely that the image has a black background and white text.
            // In this case, invert the image.
            if (whitePixels < totalPixels / 2)
            {
                Cv2.BitwiseNot(gray, dst);
            }
            else
            {
                dst = gray.Clone();
            }

            return dst;
        }

        public static Mat ScaleUp(Mat input, double factor = 200)
        {
            //scale up to 200px height
            // Calculate the scaling factor
            double scale = factor / input.Height;

            var resizedMat = new Mat();

            // Resize the image while maintaining the aspect ratio
            Cv2.Resize(input, resizedMat, new OpenCvSharp.Size(0, 0), scale, scale, InterpolationFlags.Cubic);
            return resizedMat;
        }


    }
}
