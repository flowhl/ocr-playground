using OCRPlayground.Models;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OCRPlayground.Core
{
    public class OCRResultData
    {
        public double Accuracy { get; set; }
        public string Settings { get; set; }
        public string Type { get; set; }
        public string ResultText { get; set; }
        public OCRResultData() { }
    }

    public static class ImageProcessor
    {
        public static List<OCRResultData> TopMassResults = new List<OCRResultData>();
        public static List<OCRResultData> MassResults = new List<OCRResultData>();



        public static void NoPreprocessing(OCRItem item, string progressString = null)
        {
            item.ResultImages = new List<OpenCvSharp.Mat>();
            item.ResultImages.Add(item.InputImage);
            OCRHelper.ProcessWithTesseract(item, false, "", progressString);
        }

        public static void Otsu(OCRItem item, string progressString = null)
        {
            item.ResultImages = new List<OpenCvSharp.Mat>();
            //original
            item.ResultImages.Add(item.InputImage);

            //grey
            item.ResultImages.Add(OCRHelper.ConvertToGray(item.ResultImages.Last()));

            //Otsu
            item.ResultImages.Add(OCRHelper.ApplyThreshold(item.ResultImages.Last()));

            OCRHelper.ProcessWithTesseract(item, false, "", progressString);
        }


        public static void Preprocess1(OCRItem item, string progressString = null)
        {
            item.ResultImages = new List<OpenCvSharp.Mat>();

            //original
            item.ResultImages.Add(item.InputImage);

            //denoise > makes it worse
            //item.ResultImages.Add(OCRHelper.ApplyNonLocalMeansDenoising(item.ResultImages.Last()));

            //grey
            item.ResultImages.Add(OCRHelper.ConvertToGray(item.ResultImages.Last()));

            //Otsu
            item.ResultImages.Add(OCRHelper.ApplyThreshold(item.ResultImages.Last()));

            //switch black white

            item.ResultImages.Add(OCRHelper.EnsureBlackTextOnWhite(item.ResultImages.Last()));


            OCRHelper.ProcessWithTesseract(item, false, "", progressString);
        }

        public static void TryEachSetting(OCRItem item, string progressString = null)
        {
            var possibleSettings = GenerateLimitedSettingsCombinations();
            foreach (var settings in possibleSettings)
            {
                string progress = progressString + $" settings: {possibleSettings.IndexOf(settings)}/{possibleSettings.Count}";
                ApplySettings(item, settings, progress);
            }
            var topItem = item.MassAccuracy.OrderByDescending(x => x.Item1).First();
            string mostAcc = $"Accuracy: {topItem.Item1} | Settings: {topItem.Item2}";
            TopMassResults.Add(new OCRResultData() { Accuracy = topItem.Item1, Settings = String.Copy(topItem.Item2)});
            item = null;
        }

        public static void ApplySettings(OCRItem item, Settings settings, string progressString = null)
        {
            //preprocessing
            //original
            var res = item.InputImage;

            //scaleup
            if(settings.ScaleUp != null)
            {
                res = OCRHelper.ScaleUp(res, settings.ScaleUp.Value);
            }

            //denoise
            if (settings.ApplyNonLocalMeansDenoisingSettings != null)
            {
                res = OCRHelper.ApplyNonLocalMeansDenoising(res,
                    settings.ApplyNonLocalMeansDenoisingSettings.Item1,
                    settings.ApplyNonLocalMeansDenoisingSettings.Item2,
                    settings.ApplyNonLocalMeansDenoisingSettings.Item3
                    );
            }

            if (settings.ApplyBilateralFilterSettings != null)
            {
                res = OCRHelper.ApplyBilateralFilter(res,
                    settings.ApplyBilateralFilterSettings.Item1,
                    settings.ApplyBilateralFilterSettings.Item2
                    );
            }

            if (settings.ApplyMedianFilterSettings != null && settings.ApplyMedianFilterSettings > 0)
            {
                res = OCRHelper.ApplyMedianFilter(res,
                    settings.ApplyMedianFilterSettings
                    );
            }

            if (settings.ApplyGaussianBlurSettings != null)
            {
                res = OCRHelper.ApplyGaussianBlur(res,
                    settings.ApplyGaussianBlurSettings.Item1,
                    settings.ApplyGaussianBlurSettings.Item2
                    );
            }

            //greyscale
            var grey = OCRHelper.ConvertToGray(res);

            //thresholding
            if (settings.Threshold != null)
            {
                grey = OCRHelper.ApplyThreshold(grey, settings.Threshold.Value);
            }

            //EnsureBlackText

            if (settings.EnsureBlackText)
            {
                grey = OCRHelper.EnsureBlackTextOnWhite(grey);
            }

            item.ResultImages.Add(grey);

            string ssettings = settings.GetSettings();

            OCRHelper.ProcessWithTesseract(item, true, ssettings, progressString);
        }

        /// <summary>
        /// This will generate fucking 19k possibilities
        /// </summary>
        /// <returns></returns>
        public static List<Settings> GenerateAllSettingsCombinations()
        {
            List<Settings> allSettings = new List<Settings>();

            // Iterate through each possible value for each setting property.
            foreach (var threshold in new Settings().PossibleThresoldTypes)
            {
                foreach (var blackText in new[] { true, false })
                {
                    foreach (var scaleUp in new Settings().PossibleScaleUps)
                    {
                        foreach (var nonLocalMean in new Settings().PossibleApplyNonLocalMeansDenoisingSettings)
                        {
                            foreach (var bilateralFilter in new Settings().PossibleApplyBilateralFilterSettings)
                            {
                                foreach (var medianFilter in new Settings().PossibleApplyMedianFilterSettings)
                                {
                                    foreach (var gaussianBlur in new Settings().PossibleApplyGaussianBlurSettings)
                                    {
                                        // Create a new Settings object and set its properties based on the current combination
                                        Settings setting = new Settings
                                        {
                                            Threshold = threshold,
                                            EnsureBlackText = blackText,
                                            ApplyNonLocalMeansDenoisingSettings = nonLocalMean,
                                            ApplyBilateralFilterSettings = bilateralFilter,
                                            ApplyMedianFilterSettings = medianFilter ?? 0, // Assuming default value as 0 if null
                                            ApplyGaussianBlurSettings = gaussianBlur,
                                            ScaleUp = scaleUp
                                        };

                                        // Add this settings object to the list of all possible combinations
                                        allSettings.Add(setting);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return allSettings;
        }

        /// <summary>
        /// Returns about 400 items
        /// </summary>
        /// <returns></returns>
        public static List<Settings> GenerateLimitedSettingsCombinations()
        {
            List<Settings> limitedSettings = new List<Settings>();

            // Iterate through each possible value for each selected setting property.
            foreach (var threshold in new Settings().PossibleThresoldTypes)
            {
                foreach (var blackText in new[] { true, false })
                {
                    foreach (var scaleUp in new Settings().PossibleScaleUps)
                    {
                        // NonLocalMeansDenoising only
                        foreach (var nonLocalMean in new Settings().PossibleApplyNonLocalMeansDenoisingSettings)
                        {
                            limitedSettings.Add(new Settings
                            {
                                Threshold = threshold,
                                EnsureBlackText = blackText,
                                ApplyNonLocalMeansDenoisingSettings = nonLocalMean,
                                ApplyBilateralFilterSettings = null,
                                ApplyMedianFilterSettings = 0,
                                ApplyGaussianBlurSettings = null,
                                ScaleUp = scaleUp
                            });
                        }

                        // ApplyBilateralFilter only
                        foreach (var bilateralFilter in new Settings().PossibleApplyBilateralFilterSettings)
                        {
                            limitedSettings.Add(new Settings
                            {
                                Threshold = threshold,
                                EnsureBlackText = blackText,
                                ApplyNonLocalMeansDenoisingSettings = null,
                                ApplyBilateralFilterSettings = bilateralFilter,
                                ApplyMedianFilterSettings = 0,
                                ApplyGaussianBlurSettings = null,
                                ScaleUp = scaleUp
                            });
                        }

                        // ApplyMedianFilter only
                        foreach (var medianFilter in new Settings().PossibleApplyMedianFilterSettings)
                        {
                            limitedSettings.Add(new Settings
                            {
                                Threshold = threshold,
                                EnsureBlackText = blackText,
                                ApplyNonLocalMeansDenoisingSettings = null,
                                ApplyBilateralFilterSettings = null,
                                ApplyMedianFilterSettings = medianFilter ?? 0,
                                ApplyGaussianBlurSettings = null,
                                ScaleUp = scaleUp
                            });
                        }

                        // ApplyGaussianBlur only
                        foreach (var gaussianBlur in new Settings().PossibleApplyGaussianBlurSettings)
                        {
                            limitedSettings.Add(new Settings
                            {
                                Threshold = threshold,
                                EnsureBlackText = blackText,
                                ApplyNonLocalMeansDenoisingSettings = null,
                                ApplyBilateralFilterSettings = null,
                                ApplyMedianFilterSettings = 0,
                                ApplyGaussianBlurSettings = gaussianBlur,
                                ScaleUp = scaleUp
                            });
                        }
                    }
                }
            }

            return limitedSettings;
        }

    }

    public class Settings
    {
        //Thresholds
        public List<ThresholdTypes?> PossibleThresoldTypes = new List<ThresholdTypes?>() {
            null,
            //ThresholdTypes.Binary,
            //ThresholdTypes.BinaryInv,
            //ThresholdTypes.Trunc,
            ThresholdTypes.Tozero,
            //ThresholdTypes.Mask,
            //ThresholdTypes.TozeroInv,
            ThresholdTypes.Otsu,
            //ThresholdTypes.Triangle, 
            ThresholdTypes.Otsu | ThresholdTypes.Tozero
        };
        public ThresholdTypes? Threshold { get; set; }

        //BlackText
        public bool EnsureBlackText { get; set; }

        public List<int?> PossibleScaleUps = new List<int?>()
        {
            null,
            //50,
            100,
            //150,
            //200,
        };

        public int? ScaleUp { get; set; }

        //NonLocalMeanDenoising        
        public List<Tuple<int, int, int>?> PossibleApplyNonLocalMeansDenoisingSettings = new List<Tuple<int, int, int>?>()
        {
            null,
            //new Tuple<int, int, int>(30, 7, 21),
            new Tuple<int, int, int>(3, 7, 21),
            //new Tuple<int, int, int>(2, 5, 15),
            new Tuple<int, int, int>(10, 7, 21),
            new Tuple<int, int, int>(10, 10, 30),
        };
        public Tuple<int, int, int>? ApplyNonLocalMeansDenoisingSettings { get; set; }

        //ApplyBilateralFilter
        public List<Tuple<int, double>?> PossibleApplyBilateralFilterSettings = new List<Tuple<int, double>>()
        {
            null,
            new Tuple<int, double>(9,75),
            new Tuple<int, double>(5,75),
            new Tuple<int, double>(10,50),
            new Tuple<int, double>(9,30),
        };
        public Tuple<int, double>? ApplyBilateralFilterSettings { get; set; }

        //ApplyMedianFilter
        public List<int?> PossibleApplyMedianFilterSettings = new List<int?>()
        {
            null,
            1,
            3,
            //5,
            //7,
            //9
        };
        public int ApplyMedianFilterSettings { get; set; }

        //ApplyGaussianBlur somehow cannot be straight number
        public List<Tuple<double, double>?> PossibleApplyGaussianBlurSettings = new List<Tuple<double, double>?>()
        {
            null,
            new Tuple<double, double>(3,1.5),
            new Tuple<double, double>(3,3),
            new Tuple<double, double>(5,1.5),
            //new Tuple<double, double>(11,1.5),
            //new Tuple<double, double>(5,3),
            //new Tuple<double, double>(11,3),
        };
        public Tuple<double, double>? ApplyGaussianBlurSettings { get; set; }

        public string GetSettings()
        {
            return $"{ScaleUp};{EnsureBlackText};{ApplyNonLocalMeansDenoisingSettings};{ApplyBilateralFilterSettings};{ApplyMedianFilterSettings};{ApplyGaussianBlurSettings};{Threshold}";
        }
    }
}
