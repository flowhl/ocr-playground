using OCRPlayground.Models;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tesseract;

namespace OCRPlayground.Core
{
    public static class OCRHelper
    {
        public static string ProcessWithTesseract(OCRItem item, bool useMassAccuracy = false, string settingsstring = "", string progressString = null)
        {
            if (item.InputImage.Empty()) return null;

            using (var engine = new TesseractEngine(@"D:\Github_repos\xstrat\Enterprise\xstrat-client\xstrat\External\tessdata", "naptha", EngineMode.Default))
            {
                // Limit the characters Tesseract uses for OCR
                //engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.-_,;: ");
                engine.SetVariable("tessedit_char_whitelist", "0123456789");

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
                        else if (item.InputImagePath.Contains("$score") || item.InputImagePath.ToLower().Contains("score"))
                        {
                            type = "score";
                        }
                        else if (item.InputImagePath.Contains("$teamname") || item.InputImagePath.ToLower().Contains("teamname"))
                        {
                            type = "teamname";
                        }
                        else if (item.InputImagePath.Contains("$time") || item.InputImagePath.ToLower().Contains("time"))
                        {
                            type = "time";
                        }

                        //the expected value is in the image path between $val_ and _endval
                        bool hasExpectedResult = item.InputImagePath.ToLower().Contains("$val_") && item.InputImagePath.ToLower().Contains("_endval");
                        string pattern = @"\$val_(\w+)_endval"; // Regex pattern to match the value between $val_ and _endval
                        string expectedValue = null;

                        Match match = Regex.Match(item.InputImagePath, pattern);

                        if (match.Success)
                        {
                            expectedValue = match.Groups[1].Value; // Extract the captured group
                        }

                        if (text.Length > 4 || type == "time" || type == "score")
                        {
                            lock (ImageProcessor.MassResults)
                            {
                                var result = new OCRResultData { Accuracy = page.GetMeanConfidence(), Settings = settingsstring, Type = type, ResultText = text };
                                
                                //Set accuracy to 0 if the expected value is not found
                                if (hasExpectedResult && expectedValue.IsNotNullOrEmpty())
                                {
                                    if (text.Trim().ToLower() != expectedValue.Trim().ToLower())
                                    {
                                        result.Accuracy = 0;
                                    }
                                    //else
                                    //{
                                    //    result.Accuracy = 1;
                                    //}
                                }

                                ImageProcessor.MassResults.Add(result);
                                item.MassAccuracy.Add(new Tuple<double, string>(result.Accuracy, settingsstring));
                            }
                        }
                    }

                    item.Text = text;
                    string respose = "Extracted text: " + text + " progress: " + progressString;
                    return respose;
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

        public static void PatternMatching(OCRItem item, string templateFolder)
        {
            if (item.InputImage.Empty()) return;


        }

        public static OpenCvSharp.Rect RectDimensions(OCRItem item)
        {
            Mat image = item.ResultImages.Last();

            // Find contours in the binarized image
            Cv2.FindContours(image, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            // Assuming the largest contour is the one of the number
            double maxArea = 0;
            OpenCvSharp.Rect boundingRect = new OpenCvSharp.Rect();
            foreach (var contour in contours)
            {
                var area = Cv2.ContourArea(contour);
                if (area > maxArea)
                {
                    maxArea = area;
                    boundingRect = Cv2.BoundingRect(contour);
                }
            }

            return boundingRect; // Return just the bounding rectangle

        }

        public static OpenCvSharp.Rect GetBoundingBoxOfBlackArea(Mat binarizedImage)
        {
            int minX = binarizedImage.Width;
            int minY = binarizedImage.Height;
            int maxX = 0;
            int maxY = 0;

            // Start scanning from 4 pixels in from the border to ignore the frame
            for (int y = 4; y < binarizedImage.Rows - 4; y++)
            {
                for (int x = 4; x < binarizedImage.Cols - 4; x++)
                {
                    // Assuming the number is black (pixel value == 0) against a white background
                    if (binarizedImage.At<byte>(y, x) == 0)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (minX < maxX && minY < maxY)
            {
                // Return the rectangle bounding the black area
                return new OpenCvSharp.Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
            else
            {
                // Return an empty rectangle if no black pixels were found
                return new OpenCvSharp.Rect(0, 0, 0, 0);
            }
        }


    }
}
