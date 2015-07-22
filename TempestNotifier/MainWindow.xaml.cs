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

namespace TempestNotifier
{
    class Tempest
    {
        public string name { get; set; }

        [JsonProperty(PropertyName = "base")]
        public string prefix { get; set; }

        public string suffix { get; set; }
    }

    class Map
    {
        public string name { get; set; }

        public Tempest tempest { get; set; }
    }

    public partial class MainWindow : Window
    {
        Timer timer;
        List<String> relevant_tempests = new List<String>();

        public MainWindow()
        {
            InitializeComponent();

            relevant_tempests.Add("abyssal");
            relevant_tempests.Add("shining");
            relevant_tempests.Add("radiating");
            relevant_tempests.Add("stinging");
            relevant_tempests.Add("scathing");
            relevant_tempests.Add("corrupting");
            relevant_tempests.Add("veiling");
            relevant_tempests.Add("destiny");
            relevant_tempests.Add("refining");
            relevant_tempests.Add("turmoil");
            relevant_tempests.Add("revelation");
            relevant_tempests.Add("phantoms");
            relevant_tempests.Add("animation");
            relevant_tempests.Add("inspiration");

            timer = new System.Timers.Timer(Convert.ToInt32(TimeSpan.FromMinutes(2).TotalMilliseconds));
            timer.Elapsed += Timer_Elapsed;
            timer.Enabled = false;

            update_tempests();
        }

        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            update_tempests().Wait();
        }

        public async Task update_tempests()
        {
            await Dispatcher.BeginInvoke(new Action(() =>
            {
                this.lbl_last_update.Content = "Refreshing...";
                this.btn_hard_refresh.IsEnabled = false;
            }));
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("http://poetempest.com/api/v0/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage response = await client.GetAsync("current_tempests");
                if (response.IsSuccessStatusCode)
                {
                    var json_string = await response.Content.ReadAsStringAsync();
                    Dictionary<string, Tempest> tempests = JsonConvert.DeserializeObject<Dictionary<string, Tempest>>(json_string);

                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        this.listview_maps.Items.Clear();
                    }));

                    foreach (KeyValuePair<string, Tempest> kv in tempests)
                    {
                        await Dispatcher.BeginInvoke(new Action(() =>
                        {
                            foreach (string tt in this.relevant_tempests)
                            {
                                if (kv.Value.name.ToLower().Contains(tt))
                                {
                                    this.listview_maps.Items.Add(new Map { name = kv.Key, tempest = kv.Value });
                                    break;
                                }
                            }
                        }));
                    }

                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        this.lbl_last_update.Content = String.Format("Last update: {0}", DateTime.Now.ToString());
                    }));
                }
            }

            await Dispatcher.BeginInvoke(new Action(() =>
            {
                this.btn_hard_refresh.IsEnabled = true;
            }));
        }

        private void button_Click_1(object sender, RoutedEventArgs e)
        {
            update_tempests();
        }
    }
}