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

namespace TempestNotifier
{
    class Tempest
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

        /* A combination of the maps name and level */
        public string name_lvl { get; set; }

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

    public partial class MainWindow : Window
    {
        Timer timer;
        Dictionary<String, TempestDescription> relevant_tempests;
        Dictionary<String, int> map_levels;

        public MainWindow()
        {
            InitializeComponent();

            map_levels = new Dictionary<string, int>
            {
                { "crypt", 68 }, { "desert", 68 }, { "dunes", 68 }, { "dungeon", 68 }, { "grotto", 68 }, { "pit", 68 }, { "tropical_island", 68 }, { "aqueduct", 69 }, { "arcade", 69 }, { "cemetery", 69 }, { "channel", 69 }, { "mountain_ledge", 69 }, { "sewer", 69 }, { "thicket", 69 }, { "wharf", 69 }, { "ghetto", 70 }, { "mud_geyser", 70 }, { "museum", 70 }, { "quarry", 70 }, { "reef", 70 }, { "spider_lair", 70 }, { "vaal_pyramid", 70 }, { "arena", 71 }, { "overgrown_shrine", 71 }, { "promenade", 71 }, { "shore", 71 }, { "spider_forest", 71 }, { "tunnel", 71 }, { "bog", 72 }, { "coves", 72 }, { "graveyard", 72 }, { "pier", 72 }, { "underground_sea", 72 }, { "villa", 72 }, { "arachnid_nest", 73 }, { "catacomb", 73 }, { "colonnade", 73 }, { "dry_woods", 73 }, { "strand", 73 }, { "temple", 73 }, { "jungle_valley", 74 }, { "labyrinth", 74 }, { "mine", 74 }, { "torture_chamber", 74 }, { "waste_pool", 74 }, { "canyon", 75 }, { "cells", 75 }, { "dark_forest", 75 }, { "dry_peninsula", 75 }, { "orchard", 75 }, { "arid_lake", 76 }, { "gorge", 76 }, { "residence", 76 }, { "underground_river", 76 }, { "abyss", 77 }, { "bazaar", 77 }, { "necropolis", 77 }, { "plateau", 77 }, { "academy", 78 }, { "crematorium", 78 }, { "precinct", 78 }, { "springs", 78 }, { "arsenal", 79 }, { "overgrown_ruin", 79 }, { "shipyard", 79 }, { "village_ruin", 79 }, { "courtyard", 80 }, { "excavation", 80 }, { "wasteland", 80 }, { "waterways", 80 }, { "maze", 81 }, { "palace", 81 }, { "shrine", 81 }, { "vaal_temple", 81 }, { "colosseum", 82 }, { "core", 82 }, { "volcano", 82 }
            };

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
            };

            timer = new System.Timers.Timer(Convert.ToInt32(TimeSpan.FromMinutes(2).TotalMilliseconds));
            timer.Elapsed += Timer_Elapsed;
            timer.Enabled = true;

            listview_maps.Items.SortDescriptions.Add(new SortDescription("State", ListSortDirection.Descending));

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

            using (var client = new HttpClient()) {
                client.BaseAddress = new Uri("http://poetempest.com/api/v1/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage response = await client.GetAsync("current_tempests");
                if (response.IsSuccessStatusCode) {
                    var json_string = await response.Content.ReadAsStringAsync();
                    Dictionary<string, Tempest> tempests = JsonConvert.DeserializeObject<Dictionary<string, Tempest>>(json_string);

                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        this.listview_maps.Items.Clear();
                    }));

                    List<Map> to_remove = new List<Map>();
                    foreach (KeyValuePair<string, Tempest> kv in tempests) {
                        /* Grab prefix and suffix of the tempest from the tempest name */
                        string prefix = "";
                        string suffix = "";
                        string tempest_name = kv.Value.name;
                        var tempest_parts = tempest_name.Split(new string[] { "Tempest" }, StringSplitOptions.None);
                        Console.WriteLine(tempest_name);
                        Console.WriteLine(tempest_parts.Length);
                        /* If the length of tempest_parts is not 2, the split did not find an occurance of tempest.
                           That means the name probably returned "none" or something in that manner" */
                        if (tempest_parts.Length == 2) {
                            prefix = tempest_parts[0].Trim();
                            suffix = tempest_parts[1].Trim();
                        }

                        if (prefix.Length == 0) {
                            prefix = "None";
                        }
                        if (suffix.Length == 0) {
                            suffix = "None";
                        } else {
                            suffix = suffix.TrimStart("of ".ToCharArray());
                        }

                        kv.Value.prefix = prefix;
                        kv.Value.suffix = suffix;

                        await Dispatcher.BeginInvoke(new Action(() =>
                        {
                            string tempest_string = "";
                            var map_name = String.Format("{0} ({1})", kv.Key, this.map_levels[kv.Key]);
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

                            bool added = false;
                            foreach (Map item in this.listview_maps.Items) {
                                if (item.name.Equals(map_name)) {
                                    item.tempest_description = tempest_string;
                                    item.state = state;
                                    item.votes = kv.Value.votes;
                                    item.tempest_data = kv.Value;
                                    added = true;
                                    break;
                                }
                            }
                            if (!added) {
                                this.listview_maps.Items.Add(new Map
                                {
                                    name = kv.Key,
                                    name_lvl = map_name,
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
                        foreach (Map item in to_remove) {
                            this.listview_maps.Items.Remove(item);
                        }

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
                client.BaseAddress = new Uri("http://poetempest.com/api/v1/");
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("map", map),
                    new KeyValuePair<string, string>("base", prefix),
                    new KeyValuePair<string, string>("suffix", suffix),
                });
                var result = client.PostAsync("vote", content).Result;
                string result_content = result.Content.ReadAsStringAsync().Result;
                Console.WriteLine(result_content);

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
                bool result = vote(map.name, map.tempest_data.prefix.ToLower(), map.tempest_data.suffix.ToLower()).Result;
                if (result) {
                    Console.WriteLine("Successfully voted!");
                    await update_tempests();
                } else {
                    Console.WriteLine("Error voting.");
                }
            }
        }
    }
}