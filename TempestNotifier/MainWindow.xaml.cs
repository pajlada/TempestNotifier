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

    public partial class MainWindow : Window
    {
        System.Timers.Timer timer;
        Dictionary<String, TempestDescription> relevant_tempests;
        Dictionary<string, int> map_levels;
        TempestAffixes tempest_affixes;
        TesseractEngine tesseract;
        string exe_dir;

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

            screenshot_application("PathOfExileSteam", exe_dir + "/tmp.png");

            sharpen_image(exe_dir + "/tmp.png", exe_dir + "/tmp-sharpen.png");
            threshold_image(exe_dir + "/tmp-sharpen.png", exe_dir + "/tmp-threshold.png");

            List<string> texts = new List<string>();
            string base_text = get_text(exe_dir + "/tmp.png");
            string sharpen_text = get_text(exe_dir + "/tmp-sharpen.png");
            string threshold_text = get_text(exe_dir + "/tmp-threshold.png");
            texts.Add(base_text);
            texts.Add(sharpen_text);
            texts.Add(threshold_text);

            /* XXX: For now, we only use threshold_text */
            var orig_text = threshold_text;

            //foreach (string orig_text in texts) {
            string text = orig_text.ToLower();
            foreach (Map map in listview_maps.Items) {
                if (text.Contains(map.name.Replace('_', ' '))) {
                    mt.map = map.name;
                    break;
                }
            }

            foreach (KeyValuePair<string, string> kv in tempest_affixes.prefixes) {
                if (text.Contains(kv.Key)) {
                    mt.tempest.prefix = kv.Key;
                    break;
                }
            }

            foreach (KeyValuePair<string, string> kv in tempest_affixes.suffixes) {
                if (text.Contains(kv.Key)) {
                    mt.tempest.suffix = kv.Key;
                    break;
                }
            }

            mt.tempest.name = mt.tempest.prefix + " Tempest Of " + mt.tempest.suffix;
            //}

            return mt;
        }

        public void sharpen_image(string image_path, string output_path)
        {
            using (MagickImage image = new MagickImage(image_path)) {
                //image.Blur();
                image.Sharpen(5, 30);
                //image.AdaptiveThreshold(image.Width, image.Height, 5);
                image.Write(output_path);
            }
        }

        public void threshold_image(string image_path, string output_path)
        {
            using (MagickImage image = new MagickImage(image_path)) {
                //image.Threshold(new Percentage(0.02));
                //image.Threshold(new Percentage(1));
                //image.BlackThreshold(new Percentage(0.02));

                //image.Threshold(new Percentage(0.5));

                image.BrightnessContrast(20, 20);
                image.ColorSpace = ColorSpace.Gray;
                image.Posterize(2);
                image.Normalize();
                //image.Level();
                //image.WhiteThreshold(new Percentage(0.05));
                //image.BlackThreshold(new Percentage(0.05));
                //image.ColorSpace = ColorSpace.Gray;
                //image.AdaptiveThreshold(image.Width, image.Height, 10);
                //image.Sharpen(5, 5);
                image.Write(output_path);
            }
        }

        public void screenshot_application(string procName, string output_path)
        {
            var proc = Process.GetProcessesByName(procName)[0];
            var rect = new User32.Rect();
            User32.GetWindowRect(proc.MainWindowHandle, ref rect);

            int width = rect.right - rect.left;
            int height = rect.bottom - rect.top;

            double ratio = (double)width / (double)height;

            int left_offset = (int)(width * 0.88);
            //int top_offset = (int)(height * 0.175);
            int top_offset = (int)(height * 0.045);

            width -= left_offset;
            height = (int)(height * 0.5);

            //width -= left_offset;

            var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics graphics = Graphics.FromImage(bmp);
            graphics.CopyFromScreen(rect.left + left_offset, rect.top + top_offset, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);

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
                hotkey.HotKeyPressed += async (k) =>
                {
                    MapTempest mt = get_map_tempest();
                    Console.WriteLine(String.Format("Map name: {0}", mt.map));
                    Console.WriteLine(String.Format("Tempest name: {0}", mt.tempest.name));

                    if (mt.map != "???") {
                        bool res = await vote(mt.map, mt.tempest.prefix, mt.tempest.suffix);
                        if (res) {
                            Console.WriteLine("Successfully sent tempest data!");
                            await update_tempests();
                        }
                    }
                };
            }

            exe_dir = (new FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location)).Directory.FullName;

#if DEBUG
            string tessdata_dir = exe_dir + "/../Release/tessdata/";
#else
            string tessdata_dir = exe_dir + "/tessdata/";
#endif

            try {
                tesseract = new TesseractEngine(tessdata_dir, "eng", EngineMode.Default);
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
    }
}