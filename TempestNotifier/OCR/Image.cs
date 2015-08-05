using ImageMagick;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TempestNotifier.OCR
{
    class Image
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
    }
}
