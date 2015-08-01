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
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Timers;
using System.Diagnostics;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Windows.Markup;
using Tesseract;
using System.Runtime.InteropServices;
using System.Drawing;
using ImageMagick;
using System.IO;
using FuzzyString;

namespace TempestNotifier
{
    class TempestAffix
    {
        public string name;
        public string description;

        public override string ToString()
        {
            CultureInfo culture_info = Thread.CurrentThread.CurrentCulture;
            TextInfo text_info = culture_info.TextInfo;
            return text_info.ToTitleCase(name) + " (" + description + ")";
        }
    }

    class TempestAffixes
    {
        [JsonProperty(PropertyName = "bases")]
        public Dictionary<string, string> prefixes { get; set; }

        public Dictionary<string, string> suffixes { get; set; }
    }

    public class Tempest
    {
        public string name { get; set; }

        [JsonProperty(PropertyName = "base")]
        public string prefix { get; set; }

        public string suffix { get; set; }

        public int votes { get; set; }
    }

    class Map
    {
        public string name { get; set; }

        public int level { get; set; }

        public string tempest_description { get; set; }

        public Tempest tempest_data { get; set; }

        public int state { get; set; }

        public int votes { get; set; }
    }

    class TempestDescription
    {
        public string short_description { get; set; }

        public bool good { get; set; }
    }

    public class MapTempest
    {
        public string map { get; set; }

        public Tempest tempest { get; set; }
    }

    public abstract class BaseConverter : MarkupExtension
    {
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }

    [ValueConversion(typeof(object), typeof(string))]
    public class MapFormatConverter : BaseConverter, IValueConverter
    {
        public string title_case(string name)
        {
            CultureInfo culture_info = Thread.CurrentThread.CurrentCulture;
            TextInfo text_info = culture_info.TextInfo;
            return text_info.ToTitleCase(name.Replace('_', ' '));
        }
        public object Convert(object value, Type targetType, object parameter,
                          System.Globalization.CultureInfo culture)
        {
            Map map = value as Map;
            return String.Format("{0} ({1})", title_case(map.name), map.level);
        }

        public object ConvertBack(object value, Type targetType, object parameter,
                        System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }

    public class CanUpvote : BaseConverter, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
                          System.Globalization.CultureInfo culture)
        {
            Map map = value as Map;
            return (map.tempest_data.prefix != "unknown");
        }

        public object ConvertBack(object value, Type targetType, object parameter,
                        System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }

    public class User32
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowRect(IntPtr hWnd, ref Rect rect);
    }

    public class CropData
    {
        public int width { get; set; }
        public int height { get; set; }
        public Tuple<int, int> map_crop_from;
        public Tuple<int, int> map_crop_to;
        public Tuple<int, int> tempest_crop_from;
        public Tuple<int, int> tempest_crop_to;
    }

    public partial class MainWindow : Window
    {
        System.Timers.Timer timer;
        Dictionary<String, TempestDescription> relevant_tempests;
        Dictionary<string, int> map_levels;
        TempestAffixes tempest_affixes;
        TesseractEngine tesseract;
        string exe_dir;
        MapTempest found_mt = null;

        public string get_text(string image_path)
        {
            try {
                using (var img = Pix.LoadFromFile(image_path)) {
                    using (var page = tesseract.Process(img)) {
                        var text = page.GetText();
                        return text;
                    }
                }
            } catch (Exception e) {
                Trace.TraceError(e.ToString());
                Console.WriteLine("Unexpected Error: " + e.Message);
                Console.WriteLine("Details: ");
                Console.WriteLine(e.ToString());
            }

            return "";
        }

        /* Returns file paths of images that are relevant to read */
        public List<KeyValuePair<string, string>> apply_filters(string from_path, string to_path_format, string key = "")
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

            update_label_d(lbl_report, "Sharpening {0}", key);
            sharpen_image(from_path, sharpen_path);

            update_label_d(lbl_report, "Monochroming {0} #1", key);
            threshold_image(sharpen_path, threshold_path);

            update_label_d(lbl_report, "Blurring {0}", key);
            blur_image(sharpen_path, blur_path, 0.05, 0.2);

            update_label_d(lbl_report, "Scaling {0} #1", key);
            scale_image(blur_path, scaled_path, 150, 150);

            update_label_d(lbl_report, "Monochroming {0} #2", key);
            threshold_image(scaled_path, threshold2_path, 15, 15, 2);

            update_label_d(lbl_report, "Scaling {0} #2", key);
            scale_image(blur_path, scaled2_path, 75, 100);

            update_label_d(lbl_report, "Monochroming {0} #3", key);
            threshold_image(scaled2_path, threshold3_path, 15, 15, 2);

            update_label_d(lbl_report, "Scaling {0} #3", key);
            scale_image(blur_path, scaled3_path, 150, 125);

            update_label_d(lbl_report, "Monochroming {0} #4", key);
            threshold_image(scaled3_path, threshold4_path, 15, 15, 2);

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

        public void update_label(Label lbl, string format, params object[] values)
        {
            lbl.Content = String.Format(format, values);
        }

        public async void update_label_d(Label lbl, string format, params object[] values)
        {
            await Dispatcher.BeginInvoke(new Action(() =>
            {
                update_label(lbl, format, values);
            }));
        }

        public MapTempest get_map_tempest()
        {
            MapTempest mt = new MapTempest
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

            update_label_d(lbl_report, "Screenshotting Path of Exile");
            int width, height;
            width = 2560;
            height = 1440;
            try {
                screenshot_application("PathOfExileSteam", exe_dir + "/full.png", out width, out height);
            } catch (Exception) {
                update_label_d(lbl_report, "Something went wrong while screenshotting");
                return mt;
            }

            CropData crop_data = new CropData
            {
                width = width,
                height = height,
                map_crop_from = new Tuple<int, int>(1730, 40),
                map_crop_to = new Tuple<int, int>(1910, 65),
                tempest_crop_from = new Tuple<int, int>(1560, 185),
                tempest_crop_to = new Tuple<int, int>(1920, 206),
            };

            crop_data.map_crop_from = new Tuple<int, int>((int)(width * 0.85), 0);
            crop_data.map_crop_to = new Tuple<int, int>(width, (int)(height * 0.15));
            crop_data.tempest_crop_from = new Tuple<int, int>((int)(width * 0.85), (int)(height * 0.10));
            crop_data.tempest_crop_to = new Tuple<int, int>(width, (int)(height * 0.45));

            List<CropData> preset_crop_datas = new List<CropData>
            {
                new CropData {
                    width = 1920,
                    height = 1080,
                    map_crop_from = new Tuple<int, int>(1730, 40),
                    map_crop_to = new Tuple<int, int>(1910, 65),
                    tempest_crop_from = new Tuple<int, int>(1560, 185),
                    tempest_crop_to = new Tuple<int, int>(1920, 206),
                },
                new CropData {
                    width = 2560,
                    height = 1440,
                    map_crop_from = new Tuple<int, int>(2300, 58),
                    map_crop_to = new Tuple<int, int>(2545, 88),
                    tempest_crop_from = new Tuple<int, int>(2056, 256),
                    tempest_crop_to = new Tuple<int, int>(2560, 283),
                },
            };

            foreach (var cd in preset_crop_datas) {
                if (width == cd.width && height == cd.height) {
                    crop_data = cd;
                    break;
                }
            }

            /* Crop the full screenshot into map.png and tempest.png */

            update_label_d(lbl_report, "Cropping map");
            crop_image(exe_dir + "/full.png", exe_dir + "/map.png", crop_data.map_crop_from, crop_data.map_crop_to);

            update_label_d(lbl_report, "Cropping tempest");
            crop_image(exe_dir + "/full.png", exe_dir + "/tempest.png", crop_data.tempest_crop_from, crop_data.tempest_crop_to);

            List<KeyValuePair<string, string>> map_images = apply_filters(exe_dir + "/map.png", exe_dir + "/map-{0}.png", "map");
            List<KeyValuePair<string, string>> tempest_images = apply_filters(exe_dir + "/tempest.png", exe_dir + "/tempest-{0}.png", "tempest");

            foreach (KeyValuePair<string, string> kv in map_images) {
                if (mt.map != "???") {
                    /* Stop searching if we've already found a map match */
                    break;
                }

                update_label_d(lbl_report, "Reading {0} Map image", kv.Key);
                string text = get_text(kv.Value).ToLower().Trim();
                Console.WriteLine(String.Format("{0}: '{1}'", kv.Key, text));
                update_label_d(lbl_report, "Read {0} Map image ({1})", kv.Key, text);
                foreach (Map map in listview_maps.Items) {
                    string mapname = map.name.Replace('_', ' ');
                    update_label_d(lbl_report, "Comparing {0} to {1}", text, mapname);
                    if (text.Contains(mapname)) {
                        update_label_d(lbl_report, "Setting map to {0}", mapname);
                        mt.map = map.name;
                        break;
                    }
                }
            }

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

                update_label_d(lbl_report, "Reading {0} Tempest image", kv.Key);
                string text_raw = get_text(kv.Value).ToLower().Trim();
                tempest_texts.Add(text_raw);
                string text = text_raw.Replace(" ", "");

                update_label_d(lbl_report, "Read {0} Tempest image ({1})", kv.Key, text_raw);
                foreach (KeyValuePair<string, string> tempest_kv in tempest_affixes.prefixes) {
                    if (mt.tempest.prefix != "none") {
                        break;
                    }
                    string affix = tempest_kv.Key.ToLower().Replace('_', ' ');
                    update_label_d(lbl_report, "Comparing {0} '{1}' with prefix {2}", kv.Key, text, affix);
                    if (text.Contains(affix)) {
                        update_label_d(lbl_report, "Setting prefix to {0}", affix);
                        mt.tempest.prefix = tempest_kv.Key;
                        break;
                    }
                }

                foreach (KeyValuePair<string, string> tempest_kv in tempest_affixes.suffixes) {
                    string affix = tempest_kv.Key.ToLower().Replace('_', ' ');
                    update_label_d(lbl_report, "Comparing {0} '{1}' with suffix {2}", kv.Key, text, affix);
                    if (text.Contains(affix)) {
                        update_label_d(lbl_report, "Setting suffix to {0}", affix);
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

                foreach (KeyValuePair<string, string> tempest_kv in tempest_affixes.prefixes) {
                    if (mt.tempest.prefix != "none") {
                        break;
                    }
                    string affix = tempest_kv.Key.ToLower().Replace('_', ' ');

                    foreach (string str in text_raw.Split(' ')) {
                        update_label_d(lbl_report, "Fuzzy comparing {0} with {1}", affix, str);
                        if (str.ApproximatelyEquals(affix, fuzzystringoptions, tolerance)) {
                            update_label_d(lbl_report, "Setting prefix to {0}", affix);
                            Console.WriteLine(String.Format("'{0}' matched '{1}'", str, affix));
                            mt.tempest.prefix = tempest_kv.Key;
                            break;
                        }
                    }
                }

                foreach (KeyValuePair<string, string> tempest_kv in tempest_affixes.suffixes) {
                    string affix = tempest_kv.Key.ToLower().Replace('_', ' ');

                    foreach (string str in text_raw.Split(' ')) {
                        update_label_d(lbl_report, "Fuzzy comparing {0} with {1}", affix, str);
                        if (str.ApproximatelyEquals(affix, fuzzystringoptions, tolerance)) {
                            update_label_d(lbl_report, "Setting prefix to {0}", affix);
                            Console.WriteLine(String.Format("'{0}' matched '{1}'", str, affix));
                            mt.tempest.prefix = tempest_kv.Key;
                            break;
                        }
                    }
                }
            }

            mt.tempest.name = mt.tempest.prefix + " Tempest Of " + mt.tempest.suffix;

            update_label_d(lbl_report, "Done looping!");

            return mt;
        }

        public void crop_image(string image_path, string output_path, Tuple<int, int> from, Tuple<int, int> to)
        {
            using (MagickImage image = new MagickImage(image_path)) {
                image.Crop(new MagickGeometry(from.Item1, from.Item2, to.Item1 - from.Item1, to.Item2 - from.Item2));
                image.Write(output_path);
            }
        }

        public void sharpen_image(string image_path, string output_path, double radius = 5, double sigma = 30)
        {
            using (MagickImage image = new MagickImage(image_path)) {
                image.Sharpen(radius, sigma);
                image.Write(output_path);
            }
        }

        public void blur_image(string image_path, string output_path, double width = 2, double sigma = 2)
        {
            using (MagickImage image = new MagickImage(image_path)) {
                image.GaussianBlur(width, sigma);
                image.Write(output_path);
            }
        }

        public void threshold_image(string image_path, string output_path, int brightness = 20, int contrast = 20, int posterize = 2)
        {
            using (MagickImage image = new MagickImage(image_path)) {
                image.BrightnessContrast(brightness, contrast);
                image.ColorSpace = ColorSpace.Gray;
                image.Posterize(posterize);
                image.Normalize();
                image.Write(output_path);
            }
        }

        public void scale_image(string image_path, string output_path, int width_mul = 150, int height_mul = 150)
        {
            using (MagickImage image = new MagickImage(image_path)) {
                image.Scale(new Percentage(width_mul), new Percentage(height_mul));
                image.Write(output_path);
            }
        }

        public void screenshot_application(string procName, string output_path, out int window_width, out int window_height)
        {
            var proc = Process.GetProcessesByName(procName)[0];
            var rect = new User32.Rect();
            User32.GetWindowRect(proc.MainWindowHandle, ref rect);

            int width = rect.right - rect.left;
            int height = rect.bottom - rect.top;

            window_width = width;
            window_height = height;

            var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics graphics = Graphics.FromImage(bmp);
            graphics.CopyFromScreen(rect.left, rect.top, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);

            bmp.Save(output_path, System.Drawing.Imaging.ImageFormat.Png);
        }

        public MainWindow()
        {
            InitializeComponent();

            map_levels = new Dictionary<string, int>();
            relevant_tempests = new Dictionary<string, TempestDescription>
            {
                { "abyssal", new TempestDescription { short_description = "Chaos damage", good = false } },
                { "shining", new TempestDescription { short_description = "Increased item Rarity/Quantity", good = true } },
                { "radiating", new TempestDescription { short_description = "Increased item Rarity/Quantity", good = true } },
                { "stinging", new TempestDescription { short_description = "All hits are Critical Strikes", good = false } },
                { "scathing", new TempestDescription { short_description = "All hits are Critical Strikes", good = false } },
                { "corrupting", new TempestDescription { short_description = "Corrupted drops", good = true } },
                { "veiling", new TempestDescription { short_description = "0% elemental resists", good = false } },
                { "destiny", new TempestDescription { short_description = "1 guaranteed map drop", good = true } },
                { "refining", new TempestDescription { short_description = "Quality drops", good = true } },
                { "turmoil", new TempestDescription { short_description = "20 additional rogue exiles", good = true } },
                { "revelation", new TempestDescription { short_description = "50% increased experience", good = true } },
                { "phantoms", new TempestDescription { short_description = "10 additional tormented spirits", good = true } },
                { "animation", new TempestDescription { short_description = "Weapons are animated", good = false } },
                { "inspiration", new TempestDescription { short_description = "15% increased experience", good = true } },
                { "fortune", new TempestDescription { short_description = "1 guaranteed unique item", good = true } },
                { "fate", new TempestDescription { short_description = "1 guaranteed vaal fragment", good = true } },
            };

            timer = new System.Timers.Timer(Convert.ToInt32(TimeSpan.FromMinutes(2).TotalMilliseconds));
            timer.Elapsed += Timer_Elapsed;
            timer.Enabled = true;

            listview_maps.Items.SortDescriptions.Clear();
            listview_maps.Items.SortDescriptions.Add(new SortDescription("state", ListSortDirection.Descending));
            listview_maps.Items.SortDescriptions.Add(new SortDescription("level", ListSortDirection.Ascending));
            listview_maps.Items.SortDescriptions.Add(new SortDescription("name", ListSortDirection.Ascending));

            this.cb_prefix.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler(this.cb_prefix_TextChanged));

            {
                var hotkey = new HotKey(ModifierKeys.Shift, Keys.F9, this);
                hotkey.HotKeyPressed += (k) =>
                {
                    Task.Factory.StartNew(() =>
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            lbl_report.Content = "Beginning to report current map tempest...";
                            btn_report.IsEnabled = false;
                            found_mt = null;
                        }));
                        MapTempest mt = get_map_tempest();
                        Console.WriteLine(String.Format("Map name: {0}", mt.map));
                        Console.WriteLine(String.Format("Tempest name: {0}", mt.tempest.name));
                        bool do_vote = false;

                        if (do_vote && mt.map != "???" && (mt.tempest.prefix != "none" || mt.tempest.suffix == "none")) {
                            update_label_d(lbl_report, "Reporting {0}, {1} tempest of {2}", mt.map, mt.tempest.prefix, mt.tempest.suffix);
                            bool res = vote(mt.map, mt.tempest.prefix, mt.tempest.suffix).Result;
                            if (res) {
                                Console.WriteLine("Successfully sent tempest data!");
                                update_label_d(lbl_report, "Reported {0}, {1} tempest of {2}", mt.map, mt.tempest.prefix, mt.tempest.suffix);
                                update_tempests();
                            } else {
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    lbl_report.Content = "Error reporting " + mt.map + ", " + mt.tempest.prefix + " tempest of " + mt.tempest.suffix;
                                }));
                            }
                        } else {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                lbl_report.Content = "NOT reporting " + mt.map + ", " + mt.tempest.prefix + " tempest of " + mt.tempest.suffix;
                            }));
                            found_mt = mt;
                            if (mt.map != "???" && (mt.tempest.prefix != "none" || mt.tempest.suffix == "none")) {
                                found_mt = null;
                            }
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (found_mt != null) {
                                    btn_report.IsEnabled = false;
                                } else {
                                    btn_report.IsEnabled = true;
                                }
                            }));
                        }
                    });
                };
            }

            exe_dir = (new FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location)).Directory.FullName;

#if DEBUG
            string tessdata_dir = exe_dir + "/../Release/tessdata/";
#else
            string tessdata_dir = exe_dir + "/tessdata/";
#endif

            try {
                tesseract = new TesseractEngine(tessdata_dir, "eng", EngineMode.TesseractAndCube);
            } catch (Exception e) {
                Trace.TraceError(e.ToString());
                Console.WriteLine("Unexpected Error: " + e.Message);
                Console.WriteLine("Details: ");
                Console.WriteLine(e.ToString());
            }
        }

        private async void initialize_data()
        {
            await Dispatcher.BeginInvoke(new Action(() =>
            {
                this.lbl_last_update.Content = "Fetching map levels/affixes...";
                this.btn_hard_refresh.IsEnabled = false;
            }));

            Task[] tasks = new Task[2];
            tasks[0] = update_map_levels();
            tasks[1] = update_tempest_affixes();
            Task.WaitAll(tasks);

            await update_tempests();

            await Dispatcher.BeginInvoke(new Action(() =>
            {
                update_column_width();
            }));
        }

        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            update_tempests().Wait();
        }

        public async Task update_map_levels()
        {
            this.map_levels = JsonConvert.DeserializeObject<Dictionary<string, int>>(await TempestAPI.get_raw("maps", "v0"));
        }

        public async Task update_tempest_affixes()
        {
            this.tempest_affixes = JsonConvert.DeserializeObject<TempestAffixes>(await TempestAPI.get_raw("tempests"));

            await Dispatcher.BeginInvoke(new Action(() =>
            {
                /* Populate the comboboxes with the tempest affixes. */
                this.cb_prefix.Items.Clear();
                this.cb_suffix.Items.Clear();

                foreach (KeyValuePair<string, string> affix in this.tempest_affixes.prefixes) {
                    string description = affix.Value;
                    TempestDescription td;
                    relevant_tempests.TryGetValue(affix.Key, out td);
                    if (td != null) {
                        description = td.short_description;
                    }
                    this.cb_prefix.Items.Add(new TempestAffix { name = affix.Key, description = description });
                }

                foreach (KeyValuePair<string, string> affix in this.tempest_affixes.suffixes) {
                    string description = affix.Value;
                    TempestDescription td;
                    relevant_tempests.TryGetValue(affix.Key, out td);
                    if (td != null) {
                        description = td.short_description;
                    }
                    this.cb_suffix.Items.Add(new TempestAffix { name = affix.Key, description = description });
                }
            }));
        }

        public async Task update_tempests()
        {
            Console.Write("Updating tempests...");
            await Dispatcher.BeginInvoke(new Action(() =>
            {
                this.lbl_last_update.Content = "Refreshing...";
                this.btn_hard_refresh.IsEnabled = false;
            }));

            using (var client = new HttpClient()) {
                client.BaseAddress = new Uri("http://poetempest.com/api/v1/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage response = await client.GetAsync("current_tempests");
                if (response.IsSuccessStatusCode) {
                    var json_string = await response.Content.ReadAsStringAsync();
                    Dictionary<string, Tempest> tempests = JsonConvert.DeserializeObject<Dictionary<string, Tempest>>(json_string);

                    foreach (KeyValuePair<string, Tempest> kv in tempests) {
                        string tempest_name = kv.Value.name;

                        await Dispatcher.BeginInvoke(new Action(() =>
                        {
                            string tempest_string = "";
                            int map_level = 100;
                            try {
                                map_level = this.map_levels[kv.Key];
                            } catch (Exception e) {
                                Console.WriteLine("Caught exception while trying to get a map's level: " + e);
                            }
                            int state = 0;
                            int num_matches = 0;

                            foreach (KeyValuePair<string, TempestDescription> relevant_tempest in this.relevant_tempests) {
                                if (kv.Value.name.ToLower().Contains(relevant_tempest.Key)) {
                                    num_matches++;
                                    if (tempest_string.Length > 0) {
                                        tempest_string += ", ";
                                    }
                                    tempest_string += relevant_tempest.Value.short_description;
                                    if (relevant_tempest.Value.good == false) {
                                        state -= 1;
                                    } else {
                                        state += 1;
                                    }
                                }
                            }

                            if (num_matches == 0) {
                                tempest_string = kv.Value.name;
                                state = -100;
                            }

                            Map map = listview_maps.Items.Cast<Map>().FirstOrDefault(i => i.name == kv.Key);
                            if (map != null) {
                                map.tempest_description = tempest_string;
                                map.state = state;
                                map.votes = kv.Value.votes;
                                map.tempest_data = kv.Value;
                            } else {
                                this.listview_maps.Items.Add(new Map
                                {
                                    name = kv.Key,
                                    level = map_level,
                                    tempest_description = tempest_string,
                                    tempest_data = kv.Value,
                                    state = state,
                                    votes = kv.Value.votes,
                                });
                            }
                        }));
                    }

                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        this.lbl_last_update.Content = String.Format("Last update: {0}", DateTime.Now.ToString());

                        listview_maps.Items.SortDescriptions.Add(new SortDescription("state", ListSortDirection.Descending));
                    }));
                }
            }

            await Dispatcher.BeginInvoke(new Action(() =>
            {
                this.btn_hard_refresh.IsEnabled = true;
            }));
        }

        private async Task<bool> vote(string map, string prefix, string suffix)
        {
            using (var client = new HttpClient()) {
                client.Timeout = new TimeSpan(0, 0, 2);
                client.BaseAddress = new Uri("http://poetempest.com/api/v1/");
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("map", map),
                    new KeyValuePair<string, string>("base", prefix),
                    new KeyValuePair<string, string>("suffix", suffix),
                });
                var result = await client.PostAsync("vote", content);
                string result_content = result.Content.ReadAsStringAsync().Result;

                if (result_content.Length == 0) {
                    return true;
                }
            }

            return false;
        }

        private async void button_Click_1(object sender, RoutedEventArgs e)
        {
            await update_tempests();
        }

        private async void UpvoteTempestContextMenu_on_click(object sender, RoutedEventArgs e)
        {
            Map map = (Map)listview_maps.SelectedItem;
            if (map != null) {
                await vote(map.name, map.tempest_data.prefix.ToLower(), map.tempest_data.suffix.ToLower());
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            Map map = ((FrameworkElement)sender).DataContext as Map;
            if (map != null) {
                bool result = await vote(map.name, map.tempest_data.prefix.ToLower(), map.tempest_data.suffix.ToLower());
                if (result) {
                    Console.WriteLine("Successfully voted!");
                    await update_tempests();
                } else {
                    Console.WriteLine("Error voting.");
                }
            }
        }

        private void cb_prefix_TextChanged(object sender, RoutedEventArgs e)
        {
            ComboBox cb = (ComboBox)sender;
            //Console.WriteLine(cb.Text);
        }

        private void listview_maps_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Map map = listview_maps.SelectedItem as Map;

            if (map == null) {
                cb_prefix.SelectedIndex = -1;
                cb_suffix.SelectedIndex = -1;
                btn_vote.IsEnabled = false;
                cb_prefix.IsEnabled = false;
                cb_suffix.IsEnabled = false;
            } else {
                btn_vote.IsEnabled = true;
                cb_prefix.IsEnabled = true;
                cb_suffix.IsEnabled = true;

                if (map.tempest_data.prefix == "unknown" || map.tempest_data.suffix == "unknown") {
                    cb_prefix.SelectedIndex = -1;
                    cb_suffix.SelectedIndex = -1;
                } else {
                    cb_prefix.SelectedItem = cb_prefix.Items.Cast<TempestAffix>().FirstOrDefault(affix => affix.name == map.tempest_data.prefix);
                    cb_suffix.SelectedItem = cb_suffix.Items.Cast<TempestAffix>().FirstOrDefault(affix => affix.name == map.tempest_data.suffix);
                }
            }
        }

        private async void btn_vote_Click(object sender, RoutedEventArgs e)
        {
            Map map = (Map)listview_maps.SelectedItem;

            if (map != null) {
                TempestAffix prefix = (TempestAffix)cb_prefix.SelectedItem;
                TempestAffix suffix = (TempestAffix)cb_suffix.SelectedItem;
                if (prefix == null) {
                    prefix = cb_prefix.Items.Cast<TempestAffix>().FirstOrDefault(affix => affix.name == "none");
                    cb_prefix.SelectedItem = prefix;
                }
                if (suffix == null) {
                    suffix = cb_suffix.Items.Cast<TempestAffix>().FirstOrDefault(affix => affix.name == "none");
                    cb_suffix.SelectedItem = suffix;
                }

                if (prefix != null || suffix != null) {
                    bool result = await vote(map.name, prefix.name, suffix.name);
                    if (result) {
                        Console.WriteLine("Successfully voted!");
                        await update_tempests();
                    } else {
                        Console.WriteLine("Error voting.");
                    }
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(() => initialize_data());
        }

        private void update_column_width()
        {
            ListView lv = listview_maps;
            GridView gv = lv.View as GridView;
            var actual_width = lv.ActualWidth - SystemParameters.VerticalScrollBarWidth * 2;
            for (int i = 0; i < gv.Columns.Count; ++i) {
                /* We skip the second column, because that's the column we want to fill! */
                if (i != 1) {
                    actual_width -= gv.Columns[i].ActualWidth;
                }
            }
            gv.Columns[1].Width = actual_width;
        }

        private void listview_maps_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            update_column_width();
        }

        private async void btn_report_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (found_mt == null) {
                await Dispatcher.BeginInvoke(new Action(() =>
                {
                    btn.IsEnabled = false;
                }));
                return;
            }

            bool res = vote(found_mt.map, found_mt.tempest.prefix, found_mt.tempest.suffix).Result;
            if (res) {
                Console.WriteLine("Successfully sent tempest data!");
                await Dispatcher.BeginInvoke(new Action(() =>
                {
                    lbl_report.Content = "Reported " + found_mt.map + ", " + found_mt.tempest.prefix + " tempest of " + found_mt.tempest.suffix;
                }));
                await update_tempests();
            } else {
                await Dispatcher.BeginInvoke(new Action(() =>
                {
                    lbl_report.Content = "Error reporting " + found_mt.map + ", " + found_mt.tempest.prefix + " tempest of " + found_mt.tempest.suffix;
                }));
            }
        }
    }
}