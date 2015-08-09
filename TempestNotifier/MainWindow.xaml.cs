using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

    public class CanVote : BaseConverter, IValueConverter
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

        public void begin_watching_screenshot_folder()
        {
            string my_documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string poe_subdir = "My Games\\Path of Exile\\Screenshots";
            string path;
            try {
                path = System.IO.Path.Combine(my_documents, poe_subdir);
            } catch (Exception e) {
                Console.WriteLine("Error combining {0} and {1} because {2}{3}",
                    poe_subdir, my_documents, Environment.NewLine, e.Message);
                Console.ReadKey();
                return;
            }

            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = path;
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Filter = "*.*";
            watcher.Changed += new FileSystemEventHandler(on_new_screenshot);
            watcher.EnableRaisingEvents = true;
            Console.WriteLine("Waiting for new screenshots in:\n{0}", path);
        }

        /* Uploads an image to my server for further investigation */
        static async void upload_image(string path)
        {
            Console.WriteLine("Uploading {0}", path);
            try {
                using (var client = new HttpClient()) {
                    using (var stream = File.OpenRead(path)) {
                        var content = new MultipartFormDataContent();
                        var file_content = new ByteArrayContent(new StreamContent(stream).ReadAsByteArrayAsync().Result);
                        file_content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                        file_content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                        {
                            FileName = "screenshot.png",
                            Name = "foo",
                        };
                        content.Add(file_content);
                        client.BaseAddress = new Uri("https://pajlada.se/poe/imgup/");
                        var response = await client.PostAsync("upload.php", content);
                        response.EnsureSuccessStatusCode();
                        Console.WriteLine("Done");
                    }
                }

            } catch (Exception) {
                Console.WriteLine("Something went wrong while uploading the image");
            }
        }

        public void on_new_screenshot(object sender, FileSystemEventArgs e)
        {
            Task.Factory.StartNew(() =>
            {
                Task.Factory.StartNew(() =>
                {
                    upload_image(e.FullPath);
                });
                string map_path, tempest_path;
                string map_format_path, tempest_format_path;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    lbl_report.Content = "New screenshot read...";
                    btn_report.IsEnabled = false;
                    found_mt = null;
                }));

                Bitmap img = new Bitmap(e.FullPath);
                var img_width = img.Width;
                var img_height = img.Height;

                var crop_data = new OCR.CropData
                {
                    map_from = new Tuple<int, int>((int)(img_width * 0.85), 0),
                    map_to = new Tuple<int, int>(img_width, (int)(img_height * 0.15)),
                    tempest_from = new Tuple<int, int>((int)(img_width * 0.85), (int)(img_height * 0.10)),
                    tempest_to = new Tuple<int, int>(img_width, (int)(img_height * 0.45)),
                };

                foreach (var cd in OCR.CropData.resolutions) {
                    if (img_width == cd.Key.Item1 && img_height == cd.Key.Item2) {
                        crop_data = cd.Value;
                        break;
                    }
                }

                try {
                    map_path = Path.Combine(exe_dir, "map.png");
                    tempest_path = Path.Combine(exe_dir, "tempest.png");
                    map_format_path = Path.Combine(exe_dir, "map-{0}.png");
                    tempest_format_path = Path.Combine(exe_dir, "tempest-{0}.png");
                } catch (Exception ex) {
                    Console.WriteLine("Something went wrong while combining paths. {0}", ex.Message);
                    return;
                }

                update_label_d(lbl_report, "Cropping images");
                OCR.Image.crop(e.FullPath, map_path, crop_data.map_from, crop_data.map_to);
                OCR.Image.crop(e.FullPath, tempest_path, crop_data.tempest_from, crop_data.tempest_to);

                update_label_d(lbl_report, "Applying filters to images");
                var map_images = OCR.Image.apply_filters(map_path, map_format_path);
                var tempest_images = OCR.Image.apply_filters(tempest_path, tempest_format_path);

                MapTempest mt = get_map_tempest(map_images, tempest_images);
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
                    update_label_d(lbl_report, "NOT reporting " + mt.map + ", " + mt.tempest.prefix + " tempest of " + mt.tempest.suffix);
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
        }

        public MapTempest get_map_tempest(List<KeyValuePair<string, string>> map_images, List<KeyValuePair<string, string>> tempest_images)
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

            begin_watching_screenshot_folder();
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

        private void UpvoteTempestContextMenu_on_click(object sender, RoutedEventArgs e)
        {
            Map map = (Map)listview_maps.SelectedItem;
            Map_Vote(map, map.tempest_data.prefix.ToLower(), map.tempest_data.suffix.ToLower());
        }

        private void DownvoteTempestContextMenu_on_click(object sender, RoutedEventArgs e)
        {
            Map map = (Map)listview_maps.SelectedItem;
            Map_Vote(map, "none", "none");
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Map map = ((FrameworkElement)sender).DataContext as Map;
            Map_Vote(map, map.tempest_data.prefix.ToLower(), map.tempest_data.suffix.ToLower());
        }

        private void Downvote_Button_Click(object sender, RoutedEventArgs e)
        {
            Map map = ((FrameworkElement)sender).DataContext as Map;
            listview_maps.SelectedItem = map;
            this.maingrid.RowDefinitions[1].Height = new GridLength(26);
            cb_prefix.SelectedItem = cb_prefix.Items.Cast<TempestAffix>().FirstOrDefault(affix => affix.name == "none");
            cb_suffix.SelectedItem = cb_suffix.Items.Cast<TempestAffix>().FirstOrDefault(affix => affix.name == "none");
        }

        private async void Map_Vote(Map map, string prefix, string suffix)
        {
            this.maingrid.RowDefinitions[1].Height = new GridLength(0);
            if (map == null)
            {
                return;
            }

            bool result = await vote(map.name, map.tempest_data.prefix.ToLower(), map.tempest_data.suffix.ToLower());
            if (result)
            {
                Console.WriteLine("Successfully voted!");
                await update_tempests();
            }
            else
            {
                Console.WriteLine("Error voting.");
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
            this.maingrid.RowDefinitions[1].Height = new GridLength(0);

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
            this.maingrid.RowDefinitions[1].Height = new GridLength(0);

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