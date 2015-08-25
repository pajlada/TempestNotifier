using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Diagnostics;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Windows.Markup;
using Tesseract;
using System.Runtime.InteropServices;
using System.Drawing;
using System.IO;
using FuzzyString;

namespace TempestNotifier
{

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
        static string exe_dir;
        MapTempest found_mt = null;
        static TempestShared.ErrorLog logger = new TempestShared.ErrorLog();
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

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    lbl_report.Content = "New screenshot read...";
                    btn_report.IsEnabled = false;
                    found_mt = null;
                }));

                MapTempest mt = OCR.Image.read_map_tempest(e.FullPath, listview_maps.Items.Cast<Map>().ToList());

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

        public MainWindow()
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            //Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            InitializeComponent();

            map_levels = new Dictionary<string, int>();
            relevant_tempests = new Dictionary<string, TempestDescription>
            {
                { "abyssal", new TempestDescription { short_description = "Chaos damage", good = false } },
                { "shining", new TempestDescription { short_description = "Increased item Rarity/Quantity (120/30)", good = true } },
                { "radiating", new TempestDescription { short_description = "Increased item Rarity/Quantity (420/70)", good = true } },
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
                { "incursion", new TempestDescription { short_description = "Adds 10 Invasion Bosses", good = false } },
                { "aberrance", new TempestDescription { short_description = "Nemesis/Bloodline mods", good = true } },
                { "revealing", new TempestDescription { short_description = "Drops are identified", good = true } },
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

            OCR.TesseractWrapper.init();
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try {
                Exception ex = e.Exception;
                string LogFile = logger.LogError(ex);

                Process proc = new Process();
                proc.EnableRaisingEvents = false;
                proc.StartInfo.FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorReport.exe");
                proc.StartInfo.Arguments = LogFile;
                proc.Start();
            } finally {
                Application.Current.Shutdown();
            }
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try {
                Exception ex = (Exception)e.ExceptionObject;
                string LogFile = logger.LogError(ex);

                Process proc = new Process();
                proc.EnableRaisingEvents = false;
                proc.StartInfo.FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorReport.exe");
                proc.StartInfo.Arguments = LogFile;
                proc.Start();
            } finally {
                Application.Current.Shutdown();
            }
        }

        /*
        public static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            DialogResult result = DialogResult.Abort;
            try {
                string LogFile = Logger.LogError(e.Exception);

                result = MessageBox.Show(
                    "The application encountered a error. This error has been logged and should be reported using the Error Report utility.\n\n" +
                        "Error:\n" +
                        e.Exception.Message +
                        "\n\nStack Trace:\n" +
                        e.Exception.StackTrace,
                    "Application Error",
                    MessageBoxButtons.AbortRetryIgnore,
                    MessageBoxIcon.Stop);

                Process proc = new Process();
                proc.EnableRaisingEvents = false;
                proc.StartInfo.FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorReport.exe");
                proc.StartInfo.Arguments = LogFile;
                proc.Start();
            } finally {
                if (result == DialogResult.Abort) {
                    Application.Exit();
                }
            }
        }
        */

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
            TempestAffixData.init().Wait();

            await Dispatcher.BeginInvoke(new Action(() =>
            {
                /* Populate the comboboxes with the tempest affixes. */
                this.cb_prefix.Items.Clear();
                this.cb_suffix.Items.Clear();

                foreach (KeyValuePair<string, string> affix in TempestAffixData.prefixes) {
                    string description = affix.Value;
                    TempestDescription td;
                    relevant_tempests.TryGetValue(affix.Key, out td);
                    if (td != null) {
                        description = td.short_description;
                    }
                    this.cb_prefix.Items.Add(new TempestAffix { name = affix.Key, description = description });
                }

                foreach (KeyValuePair<string, string> affix in TempestAffixData.suffixes) {
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