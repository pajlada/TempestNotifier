using FuzzyString;
using ImageMagick;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace TempestNotifier.OCR
{
    public class Image
    {
        /* Returns file paths of images that are relevant to read */
        public static List<KeyValuePair<string, string>> apply_filters(string from_path, string to_path_format, string key = "")
        {
            string sharpen_path = String.Format(to_path_format, "sharpen");
            string threshold_path = String.Format(to_path_format, "threshold");
            string blur_path = String.Format(to_path_format, "blur");
            string scaled_path = String.Format(to_path_format, "scaled");
            string threshold2_path = String.Format(to_path_format, "threshold2");
            string scaled2_path = String.Format(to_path_format, "scaled2");
            string threshold3_path = String.Format(to_path_format, "threshold3");
            string scaled3_path = String.Format(to_path_format, "scaled3");
            string threshold4_path = String.Format(to_path_format, "threshold4");

            sharpen(from_path, sharpen_path);

            threshold(sharpen_path, threshold_path);

            blur(sharpen_path, blur_path, 0.05, 0.2);

            scale(blur_path, scaled_path, 150, 150);

            threshold(scaled_path, threshold2_path, 15, 15, 2);

            scale(blur_path, scaled2_path, 75, 100);

            threshold(scaled2_path, threshold3_path, 15, 15, 2);

            scale(blur_path, scaled3_path, 150, 125);

            threshold(scaled3_path, threshold4_path, 15, 15, 2);

            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Sharpen", sharpen_path),
                new KeyValuePair<string, string>("Threshold", threshold_path),
                new KeyValuePair<string, string>("Blurred", blur_path),
                new KeyValuePair<string, string>("Scaled", scaled_path),
                new KeyValuePair<string, string>("Threshold2", threshold2_path),
                new KeyValuePair<string, string>("Scaled2", scaled2_path),
                new KeyValuePair<string, string>("Threshold3", threshold3_path),
                new KeyValuePair<string, string>("Scaled3", scaled3_path),
                new KeyValuePair<string, string>("Threshold4", threshold4_path),
            };
        }

        public static void crop(string image_path, string output_path, Tuple<int, int> from, Tuple<int, int> to)
        {
            using (MagickImage image = new MagickImage(image_path)) {
                image.Crop(new MagickGeometry(from.Item1, from.Item2, to.Item1 - from.Item1, to.Item2 - from.Item2));
                image.Write(output_path);
            }
        }

        public static void sharpen(string image_path, string output_path, double radius = 5, double sigma = 30)
        {
            using (MagickImage image = new MagickImage(image_path)) {
                image.Sharpen(radius, sigma);
                image.Write(output_path);
            }
        }

        public static void blur(string image_path, string output_path, double width = 2, double sigma = 2)
        {
            using (MagickImage image = new MagickImage(image_path)) {
                image.GaussianBlur(width, sigma);
                image.Write(output_path);
            }
        }

        public static void threshold(string image_path, string output_path, int brightness = 20, int contrast = 20, int posterize = 2)
        {
            using (MagickImage image = new MagickImage(image_path)) {
                image.BrightnessContrast(brightness, contrast);
                image.ColorSpace = ColorSpace.Gray;
                image.Posterize(posterize);
                image.Normalize();
                image.Write(output_path);
            }
        }

        public static void scale(string image_path, string output_path, int width_mul = 150, int height_mul = 150)
        {
            using (MagickImage image = new MagickImage(image_path)) {
                image.Scale(new Percentage(width_mul), new Percentage(height_mul));
                image.Write(output_path);
            }
        }

        public static MapTempest read_map_tempest(string image_path, List<Map> map_data)
        {
            string map_path, tempest_path;
            string map_format_path, tempest_format_path;

            Bitmap img = new Bitmap(image_path);
            var img_width = img.Width;
            var img_height = img.Height;

            OCR.CropData crop_data = null;

            foreach (var cd in OCR.CropData.resolutions) {
                if (img_width == cd.Key.Item1 && img_height == cd.Key.Item2) {
                    crop_data = cd.Value;
                    break;
                }
            }

            if (crop_data == null) {
                Console.WriteLine("Needs support for resolution {0}x{1}", img_width, img_height);
                crop_data = new OCR.CropData
                {
                    map_from = new Tuple<int, int>((int)(img_width * 0.85), 0),
                    map_to = new Tuple<int, int>(img_width, (int)(img_height * 0.15)),
                    tempest_from = new Tuple<int, int>((int)(img_width * 0.85), (int)(img_height * 0.10)),
                    tempest_to = new Tuple<int, int>(img_width, (int)(img_height * 0.45)),
                };
            }

            string exe_dir = (new FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location)).Directory.FullName;

            try {
                map_path = Path.Combine(exe_dir, "map.png");
                tempest_path = Path.Combine(exe_dir, "tempest.png");
                map_format_path = Path.Combine(exe_dir, "map-{0}.png");
                tempest_format_path = Path.Combine(exe_dir, "tempest-{0}.png");
            } catch (Exception ex) {
                Console.WriteLine("Something went wrong while combining paths. {0}", ex.Message);
                return null;
            }

            OCR.Image.crop(image_path, map_path, crop_data.map_from, crop_data.map_to);
            OCR.Image.crop(image_path, tempest_path, crop_data.tempest_from, crop_data.tempest_to);
            Console.Write("c");

            var map_images = OCR.Image.apply_filters(map_path, map_format_path);
            Console.Write("f");
            var tempest_images = OCR.Image.apply_filters(tempest_path, tempest_format_path);
            Console.Write("F");

            return get_map_tempest(map_images, tempest_images, map_data);
        }

        private static MapTempest get_map_tempest(List<KeyValuePair<string, string>> map_images, List<KeyValuePair<string, string>> tempest_images, List<Map> map_data)
        {
            var mt = new MapTempest
            {
                map = "???",
                tempest = new Tempest
                {
                    name = "???",
                    prefix = "none",
                    suffix = "none",
                    votes = 0
                }
            };

            foreach (KeyValuePair<string, string> kv in map_images) {
                if (mt.map != "???") {
                    /* Stop searching if we've already found a map match */
                    break;
                }

                string text = OCR.TesseractWrapper.get_text(kv.Value).ToLower().Trim();

                foreach (Map map in map_data) {
                    string mapname = map.name.Replace('_', ' ');
                    if (text.Contains(mapname)) {
                        mt.map = map.name;
                        Console.Write("m");
                        break;
                    }
                }
            }

            bool use_fuzzy_search = false;

            List<FuzzyStringComparisonOptions> fuzzystringoptions = new List<FuzzyStringComparisonOptions>
            {
                FuzzyStringComparisonOptions.UseLevenshteinDistance,
            };

            FuzzyStringComparisonTolerance tolerance = FuzzyStringComparisonTolerance.Strong;

            List<string> tempest_texts = new List<string>();

            foreach (KeyValuePair<string, string> kv in tempest_images) {
                if (mt.tempest.suffix != "none" && mt.tempest.prefix != "none") {
                    /* Stop searching if we've already found a two tempests */
                    break;
                }

                string text_raw = OCR.TesseractWrapper.get_text(kv.Value).ToLower().Trim();
                tempest_texts.Add(text_raw);
                string text = text_raw.Replace(" ", "");

                foreach (KeyValuePair<string, string> tempest_kv in TempestAffixData.prefixes) {
                    if (mt.tempest.prefix != "none") {
                        break;
                    }
                    string affix = tempest_kv.Key.ToLower().Replace('_', ' ');
                    if (text.Contains(affix)) {
                        Console.Write("p");
                        mt.tempest.prefix = tempest_kv.Key;
                        break;
                    }
                }

                foreach (KeyValuePair<string, string> tempest_kv in TempestAffixData.suffixes) {
                    string affix = tempest_kv.Key.ToLower().Replace('_', ' ');
                    if (text.Contains(affix)) {
                        Console.Write("s");
                        mt.tempest.suffix = tempest_kv.Key;
                        break;
                    }
                }
            }

            foreach (string text_raw in tempest_texts) {
                if (mt.tempest.suffix != "none" && mt.tempest.prefix != "none") {
                    /* Stop searching if we've already found a two tempests */
                    break;
                }

                if (use_fuzzy_search) {
                    foreach (KeyValuePair<string, string> tempest_kv in TempestAffixData.prefixes) {
                        if (mt.tempest.prefix != "none") {
                            break;
                        }
                        string affix = tempest_kv.Key.ToLower().Replace('_', ' ');

                        foreach (string str in text_raw.Split(' ')) {
                            if (str.ApproximatelyEquals(affix, fuzzystringoptions, tolerance)) {
                                Console.Write("p");
                                mt.tempest.prefix = tempest_kv.Key;
                                break;
                            }
                        }
                    }
                }

                if (use_fuzzy_search) {
                    foreach (KeyValuePair<string, string> tempest_kv in TempestAffixData.suffixes) {
                        string affix = tempest_kv.Key.ToLower().Replace('_', ' ');

                        foreach (string str in text_raw.Split(' ')) {
                            if (str.ApproximatelyEquals(affix, fuzzystringoptions, tolerance)) {
                                Console.Write("s");
                                mt.tempest.prefix = tempest_kv.Key;
                                break;
                            }
                        }
                    }
                }
            }

            mt.tempest.name = mt.tempest.prefix + " Tempest Of " + mt.tempest.suffix;

            return mt;
        }
    }
}
